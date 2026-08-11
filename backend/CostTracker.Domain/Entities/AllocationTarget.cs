using CostTracker.Domain.Enums;

namespace CostTracker.Domain.Entities;

public class AllocationTarget
{
    public Guid Id { get; set; }
    public Guid PortfolioId { get; set; }
    public AssetClass AssetClass { get; set; }
    public decimal Weight { get; set; }

    public InvestmentPortfolio Portfolio { get; set; } = null!;
}
