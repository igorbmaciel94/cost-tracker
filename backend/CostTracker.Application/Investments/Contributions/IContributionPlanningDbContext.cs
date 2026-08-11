using CostTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CostTracker.Application.Investments.Contributions;

/// <summary>
/// Narrow persistence seam for contribution planning. The infrastructure DbContext implements
/// this interface without exposing contribution-specific tables through the wider application
/// context contract.
/// </summary>
public interface IContributionPlanningDbContext
{
    DbSet<InvestmentPortfolio> InvestmentPortfolios { get; }
    DbSet<InvestmentInstrument> InvestmentInstruments { get; }
    DbSet<InvestmentTransaction> InvestmentTransactions { get; }
    DbSet<ManualValuation> ManualValuations { get; }
    DbSet<MarketQuoteSnapshot> MarketQuoteSnapshots { get; }
    DbSet<FxRateSnapshot> FxRateSnapshots { get; }
    DbSet<ContributionPlan> ContributionPlans { get; }
    DbSet<ContributionPlanLine> ContributionPlanLines { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
