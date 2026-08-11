namespace CostTracker.Application.Investments.MarketData;

public interface IMarketQuoteProvider
{
    string ProviderCode { get; }

    Task<ProviderBatchResult<MarketQuoteResult>> GetLatestQuotesAsync(
        IReadOnlyList<MarketQuoteRequest> requests,
        CancellationToken cancellationToken);
}
