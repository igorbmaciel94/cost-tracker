using System.Net;
using System.Text.Json;
using CostTracker.Application.Investments.MarketData;
using CostTracker.Application.Options;
using Microsoft.Extensions.Options;

namespace CostTracker.Infrastructure.Investments.MarketData;

public sealed class AlphaVantageMarketQuoteProvider(
    HttpClient httpClient,
    IOptions<MarketDataOptions> options,
    TimeProvider timeProvider) : IMarketQuoteProvider
{
    public string ProviderCode => MarketDataProviderCodes.AlphaVantage;

    public async Task<ProviderBatchResult<MarketQuoteResult>> GetLatestQuotesAsync(
        IReadOnlyList<MarketQuoteRequest> requests,
        CancellationToken cancellationToken)
    {
        if (requests.Count == 0)
            return ProviderBatchResult<MarketQuoteResult>.Empty;

        if (string.IsNullOrWhiteSpace(options.Value.AlphaVantageApiKey))
        {
            return new([], requests.Select(request => new ProviderFailure(
                ProviderCode,
                request.ProviderSymbol,
                "Alpha Vantage API key is not configured.",
                false)).ToList());
        }

        var quotes = new List<MarketQuoteResult>(requests.Count);
        var failures = new List<ProviderFailure>();

        for (var index = 0; index < requests.Count; index++)
        {
            var request = requests[index];
            if (index > 0)
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);

            try
            {
                var uri = "query?function=TIME_SERIES_DAILY" +
                          $"&symbol={Uri.EscapeDataString(request.ProviderSymbol)}" +
                          "&outputsize=compact" +
                          $"&apikey={Uri.EscapeDataString(options.Value.AlphaVantageApiKey)}";
                using var response = await httpClient.GetAsync(uri, cancellationToken);
                var payload = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    failures.Add(Failure(
                        request,
                        $"HTTP {(int)response.StatusCode} from Alpha Vantage.",
                        IsTransient(response.StatusCode)));
                    continue;
                }

                using var document = JsonDocument.Parse(payload);
                var root = document.RootElement;
                var providerMessage = MarketDataParsing.GetString(root, "Error Message", "Information", "Note");
                if (!string.IsNullOrWhiteSpace(providerMessage))
                {
                    failures.Add(Failure(request, providerMessage, root.TryGetProperty("Information", out _) || root.TryGetProperty("Note", out _)));
                    continue;
                }

                if (!root.TryGetProperty("Time Series (Daily)", out var series) ||
                    series.ValueKind != JsonValueKind.Object)
                {
                    failures.Add(Failure(request, "Alpha Vantage response has no daily time series.", false));
                    continue;
                }

                if (request.PriceMultiplier <= 0m)
                {
                    failures.Add(Failure(request, "The configured price multiplier must be positive.", false));
                    continue;
                }

                DateOnly latestDate = default;
                decimal latestClose = 0m;
                foreach (var day in series.EnumerateObject())
                {
                    if (!MarketDataParsing.TryParseDate(day.Name, out var date) ||
                        !MarketDataParsing.TryGetDecimal(day.Value, "4. close", out var close) ||
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
                    failures.Add(Failure(request, "Alpha Vantage response has no positive closing price.", false));
                    continue;
                }

                var symbol = request.ProviderSymbol;
                if (root.TryGetProperty("Meta Data", out var metadata))
                {
                    symbol = MarketDataParsing.GetString(metadata, "2. Symbol") ?? symbol;
                    if (!string.Equals(symbol, request.ProviderSymbol, StringComparison.OrdinalIgnoreCase))
                    {
                        failures.Add(Failure(
                            request,
                            $"Symbol mismatch: expected {request.ProviderSymbol}, received {symbol}.",
                            false));
                        continue;
                    }
                }

                quotes.Add(new MarketQuoteResult(
                    request.InstrumentId,
                    ProviderCode,
                    symbol,
                    request.Exchange,
                    request.Mic,
                    latestClose * request.PriceMultiplier,
                    request.ExpectedCurrency.Trim().ToUpperInvariant(),
                    "EOD_CLOSE",
                    latestDate,
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
