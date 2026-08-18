using CostTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CostTracker.Application.Interfaces;

public interface ICostTrackerDbContext
{
    DbSet<Month> Months { get; }
    DbSet<CategoryBudget> CategoryBudgets { get; }
    DbSet<Entry> Entries { get; }
    DbSet<GroupTarget> GroupTargets { get; }
    DbSet<PlanningGoal> PlanningGoals { get; }
    DbSet<HealthProfile> HealthProfiles { get; }
    DbSet<InvestmentPortfolio> InvestmentPortfolios { get; }
    DbSet<AllocationTarget> InvestmentAllocationTargets { get; }
    DbSet<InvestmentInstrument> InvestmentInstruments { get; }
    DbSet<InvestmentTransaction> InvestmentTransactions { get; }
    DbSet<ManualValuation> ManualValuations { get; }
    DbSet<MarketInstrumentMapping> MarketInstrumentMappings { get; }
    DbSet<MarketQuoteSnapshot> MarketQuoteSnapshots { get; }
    DbSet<FxRateSnapshot> FxRateSnapshots { get; }
    DbSet<DividendEvent> DividendEvents { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
