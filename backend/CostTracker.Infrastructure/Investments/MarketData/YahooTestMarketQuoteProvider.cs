using System.Net;
using System.Text.Json;
using CostTracker.Application.Investments.MarketData;
using CostTracker.Application.Options;
using Microsoft.Extensions.Options;

namespace CostTracker.Infrastructure.Investments.MarketData;

/// <summary>
/// Zero-key, best-effort adapter intended only for a personal production test.
/// It is deliberately marked as fallback and can be disabled without changing domain code.
/// </summary>
public sealed class YahooTestMarketQuoteProvider(
    HttpClient httpClient,
    IOptions<MarketDataOptions> options,
    TimeProvider timeProvider) : IMarketQuoteProvider
{
    public string ProviderCode => MarketDataProviderCodes.YahooTest;

    public async Task<ProviderBatchResult<MarketQuoteResult>> GetLatestQuotesAsync(
        IReadOnlyList<MarketQuoteRequest> requests,
        CancellationToken cancellationToken)
    {
        if (requests.Count == 0)
            return ProviderBatchResult<MarketQuoteResult>.Empty;

        if (!options.Value.EnablePublicTestQuotes)
        {
            return new([], requests.Select(request => new ProviderFailure(
                ProviderCode,
                request.ProviderSymbol,
                "The public test quote provider is disabled.",
                false)).ToList());
        }

        var quotes = new List<MarketQuoteResult>(requests.Count);
        var failures = new List<ProviderFailure>();

        foreach (var request in requests)
        {
            try
            {
                var uri = $"v8/finance/chart/{Uri.EscapeDataString(request.ProviderSymbol)}?interval=1d&range=5d&events=history";
                using var message = new HttpRequestMessage(HttpMethod.Get, uri);
                message.Headers.UserAgent.ParseAdd("CostTracker/1.0 personal-portfolio");
                using var response = await httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                var payload = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var transient = response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500;
                    failures.Add(Failure(request, $"HTTP {(int)response.StatusCode} from public test provider.", transient));
                    continue;
                }

                using var document = JsonDocument.Parse(payload);
                if (!TryReadLatest(document.RootElement, out var row, out var error))
                {
                    failures.Add(Failure(request, error, false));
                    continue;
                }

                if (request.PriceMultiplier <= 0m)
                {
                    failures.Add(Failure(request, "The configured price multiplier must be positive.", false));
                    continue;
                }

                var expectedCurrency = request.ExpectedCurrency.Trim().ToUpperInvariant();
                if (!string.Equals(row.Currency, expectedCurrency, StringComparison.OrdinalIgnoreCase) && request.PriceMultiplier == 1m)
                {
                    failures.Add(Failure(request, $"Currency mismatch: expected {expectedCurrency}, received {row.Currency}.", false));
                    continue;
                }

                quotes.Add(new MarketQuoteResult(
                    request.InstrumentId,
                    ProviderCode,
                    row.Symbol,
                    row.Exchange ?? request.Exchange,
                    request.Mic,
                    row.Price * request.PriceMultiplier,
                    expectedCurrency,
                    "LATEST_AVAILABLE",
                    row.AsOf,
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

    private static bool TryReadLatest(JsonElement root, out ParsedQuote quote, out string error)
    {
        quote = default!;
        error = "Public test provider returned no quote.";

        if (!root.TryGetProperty("chart", out var chart) ||
            !chart.TryGetProperty("result", out var results) ||
            results.ValueKind != JsonValueKind.Array ||
            results.GetArrayLength() == 0)
        {
            if (chart.ValueKind == JsonValueKind.Object &&
                chart.TryGetProperty("error", out var providerError) &&
                providerError.ValueKind == JsonValueKind.Object)
            {
                error = MarketDataParsing.GetString(providerError, "description", "code") ?? error;
            }

            return false;
        }

        var result = results[0];
        if (!result.TryGetProperty("meta", out var meta))
        {
            return false;
        }

        var symbol = MarketDataParsing.GetString(meta, "symbol");
        var currency = MarketDataParsing.NormalizeProviderCurrency(
            MarketDataParsing.GetString(meta, "currency"));
        if (string.IsNullOrWhiteSpace(symbol) || string.IsNullOrWhiteSpace(currency))
            return false;

        var exchange = MarketDataParsing.GetString(meta, "fullExchangeName", "exchangeName");
        var timezoneName = MarketDataParsing.GetString(meta, "exchangeTimezoneName");
        ParsedQuote? latestClose = null;

        if (result.TryGetProperty("timestamp", out var timestamps) &&
            timestamps.ValueKind == JsonValueKind.Array &&
            result.TryGetProperty("indicators", out var indicators) &&
            indicators.TryGetProperty("quote", out var quoteArrays) &&
            quoteArrays.ValueKind == JsonValueKind.Array &&
            quoteArrays.GetArrayLength() > 0 &&
            quoteArrays[0].TryGetProperty("close", out var closes) &&
            closes.ValueKind == JsonValueKind.Array)
        {
            var count = Math.Min(timestamps.GetArrayLength(), closes.GetArrayLength());
            for (var index = count - 1; index >= 0; index--)
            {
                var close = closes[index];
                if (close.ValueKind != JsonValueKind.Number || !close.TryGetDecimal(out var price) || price <= 0m ||
                    timestamps[index].ValueKind != JsonValueKind.Number || !timestamps[index].TryGetInt64(out var unixTimestamp) ||
                    !TryConvertTimestamp(unixTimestamp, timezoneName, out var timestamp))
                {
                    continue;
                }

                latestClose = new ParsedQuote(
                    symbol,
                    exchange,
                    currency,
                    price,
                    DateOnly.FromDateTime(timestamp.Date));
                break;
            }
        }

        ParsedQuote? regularMarketQuote = null;
        if (MarketDataParsing.TryGetDecimal(meta, "regularMarketPrice", out var regularMarketPrice) &&
            regularMarketPrice > 0m &&
            meta.TryGetProperty("regularMarketTime", out var regularMarketTime) &&
            regularMarketTime.ValueKind == JsonValueKind.Number &&
            regularMarketTime.TryGetInt64(out var regularMarketUnixTimestamp) &&
            TryConvertTimestamp(regularMarketUnixTimestamp, timezoneName, out var regularMarketTimestamp))
        {
            regularMarketQuote = new ParsedQuote(
                symbol,
                exchange,
                currency,
                regularMarketPrice,
                DateOnly.FromDateTime(regularMarketTimestamp.Date));
        }

        if (regularMarketQuote is not null &&
            (latestClose is null || regularMarketQuote.AsOf > latestClose.AsOf))
        {
            quote = regularMarketQuote;
            return true;
        }

        if (latestClose is null)
            return false;

        quote = latestClose;
        return true;
    }

    private static bool TryConvertTimestamp(
        long unixTimestamp,
        string? timezoneName,
        out DateTimeOffset timestamp)
    {
        try
        {
            timestamp = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp);
            if (!string.IsNullOrWhiteSpace(timezoneName))
            {
                try
                {
                    timestamp = TimeZoneInfo.ConvertTime(
                        timestamp,
                        TimeZoneInfo.FindSystemTimeZoneById(timezoneName));
                }
                catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
                {
                    // UTC is deterministic when the host lacks valid exchange timezone data.
                }
            }

            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            timestamp = default;
            return false;
        }
    }

    private sealed record ParsedQuote(string Symbol, string? Exchange, string Currency, decimal Price, DateOnly AsOf);
}
