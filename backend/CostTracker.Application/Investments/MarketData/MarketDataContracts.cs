namespace CostTracker.Application.Investments.MarketData;

public static class DataFreshnessCodes
{
    public const string Fresh = "FRESH";
    public const string Stale = "STALE";
    public const string Blocked = "BLOCKED";
    public const string Missing = "MISSING";
}

public sealed record MarketDataStatusDto(
    DateOnly? AsOf,
    DateTimeOffset? LastRefreshAt,
    string? Source,
    string Freshness,
    string Message,
    IReadOnlyList<Guid> StaleInstrumentIds,
    IReadOnlyList<Guid> MissingInstrumentIds,
    IReadOnlyList<ProviderFailure> Failures);

public sealed record ManualMarketQuoteRequest(
    decimal Price,
    string Currency,
    DateOnly AsOf,
    string? ProviderSymbol,
    string? Exchange,
    string? Mic);

public sealed record UpsertMarketInstrumentMappingRequest(
    string ProviderCode,
    string ProviderSymbol,
    string? Exchange,
    string? Mic,
    string QuoteCurrency,
    decimal PriceMultiplier,
    bool IsEnabled = true);

public sealed record MarketInstrumentMappingDto(
    Guid Id,
    Guid InstrumentId,
    string ProviderCode,
    string ProviderSymbol,
    string? Exchange,
    string? Mic,
    string QuoteCurrency,
    decimal PriceMultiplier,
    bool IsEnabled,
    DateTimeOffset UpdatedAt);

public sealed record DataReferenceDto(
    DateOnly? AsOf,
    DateTimeOffset? FetchedAt,
    string? Source,
    string Freshness,
    bool IsFallback);

public sealed record ValuedInvestmentPositionDto(
    Guid InstrumentId,
    long Version,
    string Name,
    string? Ticker,
    string? Mic,
    string? Isin,
    string AssetClass,
    string Kind,
    string ValuationMode,
    string NativeCurrency,
    int AllocationScore,
    decimal? Quantity,
    decimal? ManualBalance,
    decimal? CurrentPrice,
    decimal? AverageCost,
    decimal? KnownCostEur,
    decimal? ContributedEur,
    decimal? NativeValue,
    decimal? ValueEur,
    decimal? GainLossEur,
    decimal? PortfolioWeight,
    decimal? ClassWeight,
    decimal QuantityStep,
    bool Archived,
    string Freshness,
    DataReferenceDto? MarketData,
    DataReferenceDto? FxData,
    DateOnly? LastValuationAsOf);

public sealed record ValuedAllocationTargetDto(
    string AssetClass,
    decimal Weight,
    decimal CurrentWeight,
    decimal CurrentValueEur);

public sealed record PortfolioValuationSummaryDto(
    decimal TotalValueEur,
    decimal? KnownCostEur,
    decimal? GainLossEur,
    DateOnly? AsOf,
    string Freshness,
    bool IsPartial);

public sealed record ValuedPortfolioDto(
    Guid Id,
    string BaseCurrency,
    long Version,
    bool Configured,
    IReadOnlyList<ValuedAllocationTargetDto> Targets,
    PortfolioValuationSummaryDto Summary,
    IReadOnlyList<ValuedInvestmentPositionDto> Positions);
