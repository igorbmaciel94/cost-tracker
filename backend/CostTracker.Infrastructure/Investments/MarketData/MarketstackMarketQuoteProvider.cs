using System.Net;
using System.Text.Json;
using CostTracker.Application.Investments.MarketData;
using CostTracker.Application.Options;
using Microsoft.Extensions.Options;

namespace CostTracker.Infrastructure.Investments.MarketData;

public sealed class MarketstackMarketQuoteProvider(
    HttpClient httpClient,
    IOptions<MarketDataOptions> options,
    TimeProvider timeProvider) : IMarketQuoteProvider
{
    public string ProviderCode => MarketDataProviderCodes.Marketstack;

    public async Task<ProviderBatchResult<MarketQuoteResult>> GetLatestQuotesAsync(
        IReadOnlyList<MarketQuoteRequest> requests,
        CancellationToken cancellationToken)
    {
        if (requests.Count == 0)
            return ProviderBatchResult<MarketQuoteResult>.Empty;

        if (string.IsNullOrWhiteSpace(options.Value.MarketstackApiKey))
        {
            return new([], requests.Select(request => new ProviderFailure(
                ProviderCode,
                request.ProviderSymbol,
                "Marketstack API key is not configured.",
                false)).ToList());
        }

        var quotes = new List<MarketQuoteResult>(requests.Count);
        var failures = new List<ProviderFailure>();

        foreach (var request in requests)
        {
            try
            {
                var uri = $"v1/eod/latest?access_key={Uri.EscapeDataString(options.Value.MarketstackApiKey)}" +
                          $"&symbols={Uri.EscapeDataString(request.ProviderSymbol)}";
                using var response = await httpClient.GetAsync(uri, cancellationToken);
                var payload = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    failures.Add(Failure(request, $"HTTP {(int)response.StatusCode} from Marketstack.", IsTransient(response.StatusCode)));
                    continue;
                }

                using var document = JsonDocument.Parse(payload);
                var root = document.RootElement;
                if (root.TryGetProperty("error", out var error))
                {
                    failures.Add(Failure(request, MarketDataParsing.GetString(error, "message") ?? "Marketstack returned an error.", true));
                    continue;
                }

                if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
                {
                    failures.Add(Failure(request, "Marketstack response has no data array.", false));
                    continue;
                }

                var row = data.EnumerateArray().FirstOrDefault();
                if (row.ValueKind != JsonValueKind.Object ||
                    !MarketDataParsing.TryGetDecimal(row, "close", out var rawPrice) ||
                    rawPrice <= 0m)
                {
                    failures.Add(Failure(request, "Marketstack response has no positive closing price.", false));
                    continue;
                }

                if (!MarketDataParsing.TryParseDate(MarketDataParsing.GetString(row, "date"), out var asOf))
                {
                    failures.Add(Failure(request, "Marketstack response has no valid market date.", false));
                    continue;
                }

                var symbol = MarketDataParsing.GetString(row, "symbol") ?? request.ProviderSymbol;
                if (!string.Equals(symbol, request.ProviderSymbol, StringComparison.OrdinalIgnoreCase))
                {
                    failures.Add(Failure(request, $"Symbol mismatch: expected {request.ProviderSymbol}, received {symbol}.", false));
                    continue;
                }

                quotes.Add(new MarketQuoteResult(
                    request.InstrumentId,
                    ProviderCode,
                    symbol,
                    MarketDataParsing.GetString(row, "exchange", "stock_exchange") ?? request.Exchange,
                    MarketDataParsing.GetString(row, "mic") ?? request.Mic,
                    rawPrice * request.PriceMultiplier,
                    request.ExpectedCurrency.Trim().ToUpperInvariant(),
                    "EOD_CLOSE",
                    asOf,
                    timeProvider.GetUtcNow(),
                    true,
                    MarketDataParsing.Sha256(payload)));
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested &&
                                       ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                failures.Add(Failure(request, ex.Message, true));
            }
        }

        return new(quotes, failures);
    }

    private ProviderFailure Failure(MarketQuoteRequest request, string message, bool transient)
        => new(ProviderCode, request.ProviderSymbol, message, transient);

    private static bool IsTransient(HttpStatusCode statusCode)
        => statusCode == HttpStatusCode.TooManyRequests || (int)statusCode >= 500;
}
