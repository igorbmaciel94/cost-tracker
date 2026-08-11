using System.Net;
using System.Text.Json;
using CostTracker.Application.Investments.MarketData;
using CostTracker.Application.Options;
using Microsoft.Extensions.Options;

namespace CostTracker.Infrastructure.Investments.MarketData;

public sealed class TwelveDataMarketQuoteProvider(
    HttpClient httpClient,
    IOptions<MarketDataOptions> options,
    TimeProvider timeProvider) : IMarketQuoteProvider
{
    public string ProviderCode => MarketDataProviderCodes.TwelveData;

    public async Task<ProviderBatchResult<MarketQuoteResult>> GetLatestQuotesAsync(
        IReadOnlyList<MarketQuoteRequest> requests,
        CancellationToken cancellationToken)
    {
        if (requests.Count == 0)
            return ProviderBatchResult<MarketQuoteResult>.Empty;

        if (string.IsNullOrWhiteSpace(options.Value.TwelveDataApiKey))
        {
            return new([], requests.Select(request => new ProviderFailure(
                ProviderCode,
                request.ProviderSymbol,
                "Twelve Data API key is not configured.",
                false)).ToList());
        }

        var quotes = new List<MarketQuoteResult>(requests.Count);
        var failures = new List<ProviderFailure>();

        // Sequential requests keep the free/personal minute quota predictable.
        foreach (var request in requests)
        {
            try
            {
                var query = $"eod?symbol={Uri.EscapeDataString(request.ProviderSymbol)}";
                if (!string.IsNullOrWhiteSpace(request.Exchange))
                    query += $"&exchange={Uri.EscapeDataString(request.Exchange)}";

                using var message = new HttpRequestMessage(HttpMethod.Get, query);
                message.Headers.TryAddWithoutValidation("Authorization", $"apikey {options.Value.TwelveDataApiKey}");
                using var response = await httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                var payload = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    failures.Add(Failure(request, $"HTTP {(int)response.StatusCode} from Twelve Data.", IsTransient(response.StatusCode)));
                    continue;
                }

                using var document = JsonDocument.Parse(payload);
                var root = document.RootElement;
                if (string.Equals(MarketDataParsing.GetString(root, "status"), "error", StringComparison.OrdinalIgnoreCase))
                {
                    failures.Add(Failure(request, MarketDataParsing.GetString(root, "message") ?? "Twelve Data returned an error.", true));
                    continue;
                }

                if (!MarketDataParsing.TryGetDecimal(root, "close", out var rawPrice) || rawPrice <= 0m)
                {
                    failures.Add(Failure(request, "Twelve Data response has no positive closing price.", false));
                    continue;
                }

                if (request.PriceMultiplier <= 0m)
                {
                    failures.Add(Failure(request, "The configured price multiplier must be positive.", false));
                    continue;
                }

                var responseCurrency = MarketDataParsing.NormalizeProviderCurrency(
                    MarketDataParsing.GetString(root, "currency"));
                var outputCurrency = request.ExpectedCurrency.Trim().ToUpperInvariant();
                if (request.PriceMultiplier == 1m &&
                    !string.IsNullOrWhiteSpace(responseCurrency) &&
                    !string.Equals(responseCurrency, outputCurrency, StringComparison.OrdinalIgnoreCase))
                {
                    failures.Add(Failure(request, $"Currency mismatch: expected {outputCurrency}, received {responseCurrency}.", false));
                    continue;
                }

                var responseMic = MarketDataParsing.GetString(root, "mic_code", "mic");
                if (!string.IsNullOrWhiteSpace(request.Mic) &&
                    !string.IsNullOrWhiteSpace(responseMic) &&
                    !string.Equals(request.Mic, responseMic, StringComparison.OrdinalIgnoreCase))
                {
                    failures.Add(Failure(request, $"MIC mismatch: expected {request.Mic}, received {responseMic}.", false));
                    continue;
                }

                if (!MarketDataParsing.TryParseDate(MarketDataParsing.GetString(root, "datetime", "date"), out var asOf))
                {
                    failures.Add(Failure(request, "Twelve Data response has no valid market date.", false));
                    continue;
                }

                quotes.Add(new MarketQuoteResult(
                    request.InstrumentId,
                    ProviderCode,
                    MarketDataParsing.GetString(root, "symbol") ?? request.ProviderSymbol,
                    MarketDataParsing.GetString(root, "exchange") ?? request.Exchange,
                    responseMic ?? request.Mic,
                    rawPrice * request.PriceMultiplier,
                    outputCurrency,
                    "EOD_CLOSE",
                    asOf,
                    timeProvider.GetUtcNow(),
                    false,
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
