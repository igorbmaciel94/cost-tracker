using CostTracker.Domain.Enums;

namespace CostTracker.Domain.Investments.Calculations;

/// <summary>
/// Immutable input describing the EUR valuation used by one contribution calculation.
/// </summary>
public sealed record PortfolioSnapshot(
    string Version,
    IReadOnlyList<PortfolioClassSnapshot> Classes);

public sealed record PortfolioClassSnapshot(
    AssetClass AssetClass,
    decimal CurrentValueEur,
    IReadOnlyList<InstrumentSnapshot> Instruments);

/// <param name="NativeCurrencyPerEur">
/// The quote convention is always one EUR equals this many units of the native currency.
/// </param>
public sealed record InstrumentSnapshot(
    string InstrumentId,
    string Mic,
    string Symbol,
    decimal CurrentValueEur,
    decimal UnitPriceNative,
    decimal NativeCurrencyPerEur);

public readonly record struct ContributionAmount(decimal Eur);

/// <summary>
/// The allocation intent. Class targets must contain all five classes and sum exactly to one.
/// A missing instrument score has the same meaning as score zero.
/// </summary>
public sealed record AllocationPolicy(
    string Version,
    IReadOnlyList<ClassAllocationTarget> ClassTargets,
    IReadOnlyList<InstrumentAllocationScore> InstrumentScores);

public sealed record ClassAllocationTarget(
    AssetClass AssetClass,
    decimal TargetWeight);

public sealed record InstrumentAllocationScore(
    string InstrumentId,
    int Score);

/// <summary>
/// Quantity steps are expressed in units of the instrument. An override replaces the default.
/// </summary>
public sealed record ExecutionConstraints(
    decimal DefaultQuantityStep,
    IReadOnlyList<InstrumentExecutionConstraint> InstrumentOverrides);

public sealed record InstrumentExecutionConstraint(
    string InstrumentId,
    decimal QuantityStep);

public sealed record ContributionPlan(
    string AlgorithmVersion,
    string PortfolioVersion,
    string PolicyVersion,
    decimal AvailableAmountEur,
    decimal TotalRecommendedEur,
    decimal ResidualEur,
    IReadOnlyList<ClassContributionPlanLine> ClassLines,
    IReadOnlyList<InstrumentContributionPlanLine> InstrumentLines,
    IReadOnlyList<ContributionExplanation> Explanations);

public sealed record ClassContributionPlanLine(
    AssetClass AssetClass,
    decimal ValueBeforeEur,
    decimal TargetWeight,
    decimal TargetValueAfterContributionEur,
    decimal GapBeforeAllocationEur,
    decimal PlannedContributionEur,
    decimal RecommendedContributionEur,
    decimal ProjectedValueEur,
    decimal ProjectedDeviationEur,
    decimal ResidualEur,
    IReadOnlyList<ContributionExplanation> Explanations);

public sealed record InstrumentContributionPlanLine(
    AssetClass AssetClass,
    string InstrumentId,
    string Mic,
    string Symbol,
    decimal ValueBeforeEur,
    int Score,
    decimal TargetWeightWithinClass,
    decimal TargetValueAfterContributionEur,
    decimal GapBeforeAllocationEur,
    decimal PlannedContributionEur,
    decimal RecommendedContributionEur,
    decimal RecommendedAmountNative,
    decimal SuggestedQuantity,
    decimal QuantityStep,
    decimal ProjectedValueEur,
    decimal ProjectedDeviationEur,
    IReadOnlyList<ContributionExplanation> Explanations);

public sealed record ContributionExplanation(
    ContributionExplanationCode Code,
    string Message);

public enum ContributionExplanationCode
{
    MovesClassTowardTarget,
    ClassReceivesNoContribution,
    DistributedByScoreAndGap,
    ScoreZeroExcluded,
    FixedIncomeRequiresManualSelection,
    TargetOnlyClass,
    NoEligibleInstrument,
    RoundedDownToQuantityStep,
    ResidualReinvested,
    ResidualCouldNotBuyFullStep,
    FeesAndTaxesNotIncluded
}
