namespace CostTracker.Application.Contracts;

public sealed record ContributionPlanLineDto(
    Guid Id,
    string AssetClass,
    Guid? InstrumentId,
    string? InstrumentName,
    string? Ticker,
    string? NativeCurrency,
    decimal CurrentValueEur,
    decimal TargetWeight,
    decimal RecommendedAmountEur,
    decimal? RecommendedNativeAmount,
    decimal? SuggestedQuantity,
    decimal? UnitPrice,
    int? AllocationScore,
    string Explanation,
    DateOnly? QuoteAsOf,
    DateOnly? FxAsOf,
    string Freshness);

public sealed record ContributionPlanDto(
    Guid Id,
    string Status,
    decimal ContributionAmountEur,
    decimal TotalSuggestedEur,
    decimal ResidualAmountEur,
    long PortfolioVersion,
    string StrategyVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    IReadOnlyList<ContributionPlanLineDto> Lines);

public sealed class CreateContributionPlanRequest
{
    public decimal ContributionAmountEur { get; set; }
    public bool AllowStaleData { get; set; }
}

public sealed class ConfirmContributionPlanRequest
{
    public string IdempotencyKey { get; set; } = string.Empty;
    public IReadOnlyList<ContributionExecutionLineRequest> Executions { get; set; } = [];
}

public sealed class ContributionExecutionLineRequest
{
    public Guid PlanLineId { get; set; }
    public Guid? InstrumentId { get; set; }
    public DateOnly OccurredOn { get; set; }
    public decimal ActualAmountEur { get; set; }
    public decimal? ActualNativeAmount { get; set; }
    public decimal? ActualQuantity { get; set; }
    public decimal? ActualUnitPrice { get; set; }
    public decimal? Fees { get; set; }
    public string? Currency { get; set; }
}
