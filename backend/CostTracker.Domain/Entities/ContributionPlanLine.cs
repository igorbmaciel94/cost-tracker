using CostTracker.Domain.Enums;

namespace CostTracker.Domain.Entities;

/// <summary>
/// Audit snapshot of one executable recommendation. Nullable instrument fields represent a
/// fixed-income class whose concrete destination must be supplied during confirmation.
/// </summary>
public class ContributionPlanLine
{
    public Guid Id { get; set; }
    public Guid ContributionPlanId { get; set; }
    public AssetClass AssetClass { get; set; }
    public Guid? InstrumentId { get; set; }
    public string? InstrumentName { get; set; }
    public string? Ticker { get; set; }
    public string? NativeCurrency { get; set; }
    public decimal CurrentValueEur { get; set; }
    public decimal TargetWeight { get; set; }
    public decimal RecommendedAmountEur { get; set; }
    public decimal? RecommendedNativeAmount { get; set; }
    public decimal? SuggestedQuantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public int? AllocationScore { get; set; }
    public string Explanation { get; set; } = string.Empty;
    public Guid? QuoteSnapshotId { get; set; }
    public DateOnly? QuoteAsOf { get; set; }
    public Guid? FxSnapshotId { get; set; }
    public DateOnly? FxAsOf { get; set; }
    public decimal? NativeCurrencyPerEur { get; set; }
    public ContributionDataFreshness Freshness { get; set; }

    public ContributionPlan ContributionPlan { get; set; } = null!;
    public InvestmentInstrument? Instrument { get; set; }
}
