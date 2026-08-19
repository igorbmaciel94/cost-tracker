using System.Net;
using System.Text.Json;
using CostTracker.Application.Investments.MarketData;
using CostTracker.Application.Options;
using Microsoft.Extensions.Options;

namespace CostTracker.Infrastructure.Investments.MarketData;

public sealed class EodhdMarketQuoteProvider(
    HttpClient httpClient,
    IOptions<MarketDataOptions> options,
    TimeProvider timeProvider) : IMarketQuoteProvider
{
    public string ProviderCode => MarketDataProviderCodes.Eodhd;

    public async Task<ProviderBatchResult<MarketQuoteResult>> GetLatestQuotesAsync(
        IReadOnlyList<MarketQuoteRequest> requests,
        CancellationToken cancellationToken)
    {
        if (requests.Count == 0)
            return ProviderBatchResult<MarketQuoteResult>.Empty;

        if (string.IsNullOrWhiteSpace(options.Value.EodhdApiKey))
        {
            return new([], requests.Select(request => new ProviderFailure(
                ProviderCode,
                request.ProviderSymbol,
                "EODHD API key is not configured.",
                false)).ToList());
        }

        var quotes = new List<MarketQuoteResult>(requests.Count);
        var failures = new List<ProviderFailure>();
        var from = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime).AddDays(-30);

        foreach (var request in requests)
        {
            try
            {
                var uri = $"eod/{Uri.EscapeDataString(request.ProviderSymbol)}" +
                          $"?api_token={Uri.EscapeDataString(options.Value.EodhdApiKey)}" +
                          "&fmt=json" +
                          $"&from={from:yyyy-MM-dd}";
                using var response = await httpClient.GetAsync(uri, cancellationToken);
                var payload = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    failures.Add(Failure(
                        request,
                        $"HTTP {(int)response.StatusCode} from EODHD.",
                        IsTransient(response.StatusCode)));
                    continue;
                }

                using var document = JsonDocument.Parse(payload);
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Array)
                {
                    var message = root.ValueKind == JsonValueKind.Object
                        ? MarketDataParsing.GetString(root, "message", "error")
                        : null;
                    failures.Add(Failure(request, message ?? "EODHD response has no data array.", false));
                    continue;
                }

                if (request.PriceMultiplier <= 0m)
                {
                    failures.Add(Failure(request, "The configured price multiplier must be positive.", false));
                    continue;
                }

                DateOnly latestDate = default;
                decimal latestClose = 0m;
                foreach (var row in root.EnumerateArray())
                {
                    if (row.ValueKind != JsonValueKind.Object ||
                        !MarketDataParsing.TryParseDate(MarketDataParsing.GetString(row, "date"), out var date) ||
                        !MarketDataParsing.TryGetDecimal(row, "close", out var close) ||
                        close <= 0m ||
                        date <= latestDate)
                    {
                        continue;
                    }

                    latestDate = date;
                    latestClose = close;
                }

                if (latestDate == default || latestClose <= 0m)
                {
                    failures.Add(Failure(request, "EODHD response has no positive closing price.", false));
                    continue;
                }

                quotes.Add(new MarketQuoteResult(
                    request.InstrumentId,
                    ProviderCode,
                    request.ProviderSymbol,
                    request.Exchange,
                    request.Mic,
                    latestClose * request.PriceMultiplier,
                    request.ExpectedCurrency.Trim().ToUpperInvariant(),
                    "EOD_CLOSE",
                    latestDate,
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
