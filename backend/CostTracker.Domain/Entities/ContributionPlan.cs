using CostTracker.Domain.Enums;

namespace CostTracker.Domain.Entities;

/// <summary>
/// An immutable contribution preview plus its mutable lifecycle state. All monetary inputs
/// used by the calculation are copied to the child lines so a later market refresh cannot
/// rewrite the recommendation that the user saw.
/// </summary>
public class ContributionPlan
{
    public Guid Id { get; set; }
    public Guid PortfolioId { get; set; }
    public long PortfolioVersion { get; set; }
    public string PolicyVersion { get; set; } = string.Empty;
    public string StrategyVersion { get; set; } = string.Empty;
    public ContributionPlanStatus Status { get; set; } = ContributionPlanStatus.Draft;
    public decimal ContributionAmountEur { get; set; }
    public decimal TotalSuggestedEur { get; set; }
    public decimal ResidualAmountEur { get; set; }
    public bool AllowedStaleData { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public string? ConfirmationIdempotencyKey { get; set; }

    public InvestmentPortfolio Portfolio { get; set; } = null!;
    public ICollection<ContributionPlanLine> Lines { get; set; } = new List<ContributionPlanLine>();
}
