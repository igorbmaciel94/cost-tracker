using System.Net;
using System.Text;
using CostTracker.Application.Investments.MarketData;

namespace CostTracker.Infrastructure.Investments.MarketData;

public sealed class EcbExchangeRateProvider(HttpClient httpClient, TimeProvider timeProvider) : IExchangeRateProvider
{
    public string ProviderCode => MarketDataProviderCodes.Ecb;

    public async Task<ProviderBatchResult<ExchangeRateResult>> GetLatestRatesAsync(
        IReadOnlyCollection<string> quoteCurrencies,
        DateOnly asOf,
        CancellationToken cancellationToken)
    {
        var requested = quoteCurrencies
            .Select(currency => currency.Trim().ToUpperInvariant())
            .Where(currency => currency.Length == 3)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (requested.Count == 0)
            return ProviderBatchResult<ExchangeRateResult>.Empty;

        var fetchedAt = timeProvider.GetUtcNow();
        var rates = new List<ExchangeRateResult>();
        if (requested.Remove("EUR"))
        {
            rates.Add(new ExchangeRateResult(
                ProviderCode, "EUR", "EUR", 1m, "IDENTITY", asOf, fetchedAt, false, MarketDataParsing.Sha256("EUR=1")));
        }

        if (requested.Count == 0)
            return new(rates, []);

        var failures = new List<ProviderFailure>();
        try
        {
            var currencyKey = string.Join('+', requested.Select(Uri.EscapeDataString));
            var uri = $"service/data/EXR/D.{currencyKey}.EUR.SP00.A" +
                      $"?startPeriod={asOf.AddDays(-10):yyyy-MM-dd}&endPeriod={asOf:yyyy-MM-dd}" +
                      "&lastNObservations=1&format=csvdata";
            using var response = await httpClient.GetAsync(uri, cancellationToken);
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var transient = response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500;
                return new(rates, requested.Select(currency => new ProviderFailure(
                    ProviderCode, $"EUR/{currency}", $"HTTP {(int)response.StatusCode} from ECB.", transient)).ToList());
            }

            var payloadHash = MarketDataParsing.Sha256(payload);
            var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in payload.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Skip(1))
            {
                // The fields needed here precede the quoted descriptive CSV columns.
                var columns = line.Split(',', 9);
                if (columns.Length < 8)
                    continue;

                var currency = columns[2].Trim().ToUpperInvariant();
                var denominator = columns[3].Trim().ToUpperInvariant();
                if (!string.Equals(denominator, "EUR", StringComparison.OrdinalIgnoreCase) ||
                    !decimal.TryParse(columns[7], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var rate) || rate <= 0m ||
                    !DateOnly.TryParse(columns[6], out var rateDate))
                {
                    continue;
                }

                rates.Add(new ExchangeRateResult(
                    ProviderCode, "EUR", currency, rate, "ECB_REFERENCE", rateDate, fetchedAt, false, payloadHash));
                found.Add(currency);
            }

            failures.AddRange(requested
                .Where(currency => !found.Contains(currency))
                .Select(currency => new ProviderFailure(
                    ProviderCode, $"EUR/{currency}", "ECB returned no observation for the requested period.", false)));
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested &&
                                   ex is HttpRequestException or TaskCanceledException or DecoderFallbackException)
        {
            failures.AddRange(requested.Select(currency => new ProviderFailure(
                ProviderCode, $"EUR/{currency}", ex.Message, true)));
        }

        return new(rates, failures);
    }
}
