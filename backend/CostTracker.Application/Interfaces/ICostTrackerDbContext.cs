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
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
