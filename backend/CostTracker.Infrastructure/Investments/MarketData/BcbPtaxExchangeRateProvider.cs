using System.Globalization;
using System.Net;
using System.Text.Json;
using CostTracker.Application.Investments.MarketData;

namespace CostTracker.Infrastructure.Investments.MarketData;

public sealed class BcbPtaxExchangeRateProvider(HttpClient httpClient, TimeProvider timeProvider) : IExchangeRateProvider
{
    public string ProviderCode => MarketDataProviderCodes.BcbPtax;

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
        var failures = new List<ProviderFailure>();

        if (requested.Remove("EUR"))
        {
            rates.Add(new ExchangeRateResult(
                ProviderCode, "EUR", "EUR", 1m, "IDENTITY", asOf, fetchedAt, true, MarketDataParsing.Sha256("EUR=1")));
        }

        if (requested.Count == 0)
            return new(rates, failures);

        var requiredPtaxCurrencies = requested
            .Where(currency => !string.Equals(currency, "BRL", StringComparison.OrdinalIgnoreCase))
            .Append("EUR")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var observations = new Dictionary<string, PtaxObservation>(StringComparer.OrdinalIgnoreCase);
        foreach (var currency in requiredPtaxCurrencies)
        {
            try
            {
                var observation = await FetchLatestClosingAsync(currency, asOf, cancellationToken);
                if (observation is not null)
                    observations[currency] = observation;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested &&
                                       ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                failures.Add(new ProviderFailure(ProviderCode, currency, ex.Message, true));
            }
        }

        if (!observations.TryGetValue("EUR", out var euro))
        {
            failures.AddRange(requested.Select(currency => new ProviderFailure(
                ProviderCode, $"EUR/{currency}", "BCB returned no EUR closing PTAX observation.", false)));
            return new(rates, failures);
        }

        foreach (var currency in requested)
        {
            if (string.Equals(currency, "BRL", StringComparison.OrdinalIgnoreCase))
            {
                rates.Add(CreateRate(currency, euro.BrlPerCurrency, euro.AsOf, euro.RawPayload, fetchedAt));
                continue;
            }

            if (!observations.TryGetValue(currency, out var quote))
            {
                failures.Add(new ProviderFailure(
                    ProviderCode, $"EUR/{currency}", $"BCB returned no {currency} closing PTAX observation.", false));
                continue;
            }

            rates.Add(CreateRate(
                currency,
                euro.BrlPerCurrency / quote.BrlPerCurrency,
                euro.AsOf < quote.AsOf ? euro.AsOf : quote.AsOf,
                euro.RawPayload + quote.RawPayload,
                fetchedAt));
        }

        return new(rates, failures);
    }

    private ExchangeRateResult CreateRate(
        string quoteCurrency,
        decimal rate,
        DateOnly asOf,
        string rawPayload,
        DateTimeOffset fetchedAt)
        => new(
            ProviderCode,
            "EUR",
            quoteCurrency,
            rate,
            "BCB_PTAX_CLOSE",
            asOf,
            fetchedAt,
            true,
            MarketDataParsing.Sha256(rawPayload));

    private async Task<PtaxObservation?> FetchLatestClosingAsync(
        string currency,
        DateOnly asOf,
        CancellationToken cancellationToken)
    {
        var initialDate = asOf.AddDays(-10).ToString("MM-dd-yyyy", CultureInfo.InvariantCulture);
        var finalDate = asOf.ToString("MM-dd-yyyy", CultureInfo.InvariantCulture);
        var uri = "CotacaoMoedaPeriodo(moeda=@moeda,dataInicial=@dataInicial,dataFinalCotacao=@dataFinalCotacao)" +
                  $"?@moeda='{Uri.EscapeDataString(currency)}'" +
                  $"&@dataInicial='{initialDate}'&@dataFinalCotacao='{finalDate}'" +
                  "&%24top=20&%24orderby=dataHoraCotacao%20desc&%24format=json";

        using var response = await httpClient.GetAsync(uri, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var transient = response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500;
            throw new HttpRequestException(
                $"HTTP {(int)response.StatusCode} from BCB PTAX.",
                null,
                transient ? response.StatusCode : null);
        }

        using var document = JsonDocument.Parse(payload);
        if (!document.RootElement.TryGetProperty("value", out var values) || values.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var row in values.EnumerateArray())
        {
            if (!string.Equals(MarketDataParsing.GetString(row, "tipoBoletim"), "Fechamento", StringComparison.OrdinalIgnoreCase) ||
                !MarketDataParsing.TryGetDecimal(row, "cotacaoVenda", out var brlPerCurrency) || brlPerCurrency <= 0m ||
                !DateTime.TryParse(MarketDataParsing.GetString(row, "dataHoraCotacao"), CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeLocal, out var timestamp))
            {
                continue;
            }

            return new PtaxObservation(brlPerCurrency, DateOnly.FromDateTime(timestamp), payload);
        }

        return null;
    }

    private sealed record PtaxObservation(decimal BrlPerCurrency, DateOnly AsOf, string RawPayload);
}
