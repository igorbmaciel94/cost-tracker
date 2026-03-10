using CostTracker.Domain.Constants;
using CostTracker.Domain.Entities;
using CostTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CostTracker.Infrastructure.Seed;

public class CanonicalDataBackfillService(
    CostTrackerDbContext dbContext,
    ILogger<CanonicalDataBackfillService> logger)
{
    public async Task ApplyAsync(CancellationToken cancellationToken = default)
    {
        var months = await dbContext.Months
            .Include(month => month.CategoryBudgets)
            .Include(month => month.GroupTargets)
            .ToListAsync(cancellationToken);

        var hasChanges = false;

        foreach (var month in months)
        {
            hasChanges |= NormalizeCategories(month.CategoryBudgets);
            hasChanges |= NormalizeTargets(month.Id, month.GroupTargets);
        }

        if (!hasChanges)
        {
            return;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Canonical group backfill applied to existing data.");
    }

    private bool NormalizeCategories(ICollection<CategoryBudget> categories)
    {
        var hasChanges = false;

        foreach (var category in categories)
        {
            var normalizedGroupName = GroupNames.Normalize(category.GroupName);
            if (!string.Equals(category.GroupName, normalizedGroupName, StringComparison.Ordinal))
            {
                category.GroupName = normalizedGroupName;
                hasChanges = true;
            }
        }

        foreach (var category in categories)
        {
            if (!string.Equals(category.Name, "Estudos", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var normalizedName = CategoryNames.Normalize(category.Name);
            var hasConflict = categories.Any(other =>
                other.Id != category.Id &&
                string.Equals(other.Name, normalizedName, StringComparison.OrdinalIgnoreCase));

            if (hasConflict)
            {
                continue;
            }

            category.Name = normalizedName;
            hasChanges = true;
        }

        return hasChanges;
    }

    private bool NormalizeTargets(Guid monthId, ICollection<GroupTarget> targets)
    {
        var hasChanges = false;
        var deletedTargetIds = new HashSet<Guid>();

        foreach (var group in targets
                     .GroupBy(target => GroupNames.Normalize(target.GroupName), StringComparer.OrdinalIgnoreCase)
                     .ToList())
        {
            var canonicalGroupName = GroupNames.Normalize(group.Key);
            var keeper = group.FirstOrDefault(target =>
                             string.Equals(target.GroupName, canonicalGroupName, StringComparison.OrdinalIgnoreCase))
                         ?? group.First();

            if (!string.Equals(keeper.GroupName, canonicalGroupName, StringComparison.Ordinal))
            {
                keeper.GroupName = canonicalGroupName;
                hasChanges = true;
            }

            foreach (var duplicate in group.Where(target => target.Id != keeper.Id).ToList())
            {
                dbContext.GroupTargets.Remove(duplicate);
                deletedTargetIds.Add(duplicate.Id);
                hasChanges = true;
            }
        }

        var currentGroupNames = targets
            .Where(target => !deletedTargetIds.Contains(target.Id))
            .Select(target => GroupNames.Normalize(target.GroupName))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var groupName in GroupNames.All)
        {
            if (currentGroupNames.Contains(groupName))
            {
                continue;
            }

            dbContext.GroupTargets.Add(new GroupTarget
            {
                Id = Guid.NewGuid(),
                MonthId = monthId,
                GroupName = groupName,
                TargetPercent = 0m
            });
            hasChanges = true;
        }

        return hasChanges;
    }
}
