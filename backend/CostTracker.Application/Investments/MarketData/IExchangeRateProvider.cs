namespace CostTracker.Application.Investments.MarketData;

public interface IExchangeRateProvider
{
    string ProviderCode { get; }

    Task<ProviderBatchResult<ExchangeRateResult>> GetLatestRatesAsync(
        IReadOnlyCollection<string> quoteCurrencies,
        DateOnly asOf,
        CancellationToken cancellationToken);
}
