using CostTracker.Domain.Entities;

namespace CostTracker.Domain.Calculations;

public static class MonthCalculations
{
    public static IReadOnlyList<BudgetLineComputation> ComputeBudgetLines(
        IEnumerable<CategoryBudget> categories,
        IEnumerable<Entry> entries)
    {
        var spentByCategory = entries
            .GroupBy(x => x.CategoryBudgetId)
            .ToDictionary(x => x.Key, x => x.Sum(y => y.Amount));

        return categories
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Name)
            .Select(category =>
            {
                var spent = spentByCategory.GetValueOrDefault(category.Id);
                var difference = category.PlannedAmount - spent;

                return new BudgetLineComputation(
                    category.Id,
                    category.Name,
                    category.GroupName,
                    category.PlannedAmount,
                    spent,
                    difference,
                    category.DisplayOrder);
            })
            .ToList();
    }

    public static decimal ComputePlannedTotal(IEnumerable<CategoryBudget> categories)
    {
        return categories.Sum(x => x.PlannedAmount);
    }

    public static decimal ComputeSpentTotal(IEnumerable<Entry> entries)
    {
        return entries.Sum(x => x.Amount);
    }

    public static IReadOnlyList<GroupMetricComputation> ComputeGroupMetrics(
        IEnumerable<CategoryBudget> categories,
        IEnumerable<Entry> entries,
        IEnumerable<GroupTarget> targets)
    {
        var categoryList = categories.ToList();
        var entryList = entries.ToList();
        var targetList = targets.ToList();

        var plannedTotal = ComputePlannedTotal(categoryList);
        var spentTotal = ComputeSpentTotal(entryList);

        var spentByCategory = entryList
            .GroupBy(x => x.CategoryBudgetId)
            .ToDictionary(x => x.Key, x => x.Sum(y => y.Amount));

        return targetList
            .OrderBy(x => x.GroupName)
            .Select(target =>
            {
                var groupCategories = categoryList
                    .Where(x => string.Equals(x.GroupName, target.GroupName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var plannedGroup = groupCategories.Sum(x => x.PlannedAmount);
                var spentGroup = groupCategories.Sum(x => spentByCategory.GetValueOrDefault(x.Id));

                var currentPlannedPercent = plannedTotal == 0 ? 0 : plannedGroup / plannedTotal;
                var currentSpentPercent = spentTotal == 0 ? 0 : spentGroup / spentTotal;

                var plannedDiff = currentPlannedPercent - target.TargetPercent;
                var spentDiff = currentSpentPercent - target.TargetPercent;

                return new GroupMetricComputation(
                    target.GroupName,
                    target.TargetPercent,
                    currentPlannedPercent,
                    currentSpentPercent,
                    plannedDiff,
                    ResolveStatus(plannedDiff),
                    spentDiff,
                    ResolveStatus(spentDiff),
                    spentGroup);
            })
            .ToList();
    }

    public static IReadOnlyList<GroupRemainingComputation> ComputeRemainingByGroup(
        IEnumerable<CategoryBudget> categories,
        IEnumerable<Entry> entries)
    {
        return ComputeBudgetLines(categories, entries)
            .GroupBy(x => x.GroupName, StringComparer.OrdinalIgnoreCase)
            .Select(group => new GroupRemainingComputation(
                group.First().GroupName,
                group.Sum(x => x.Difference)))
            .OrderBy(x => x.GroupName)
            .ToList();
    }

    public static string ResolveStatus(decimal difference)
    {
        if (Math.Abs(difference) <= 0.005m)
        {
            return "OK";
        }

        return difference > 0 ? "Acima" : "Abaixo";
    }
}

public sealed record BudgetLineComputation(
    Guid CategoryId,
    string CategoryName,
    string GroupName,
    decimal Planned,
    decimal Spent,
    decimal Difference,
    int DisplayOrder);

public sealed record GroupMetricComputation(
    string GroupName,
    decimal TargetPercent,
    decimal CurrentPlannedPercent,
    decimal CurrentSpentPercent,
    decimal PlannedDifference,
    string PlannedStatus,
    decimal SpentDifference,
    string SpentStatus,
    decimal SpentAmount);

public sealed record GroupRemainingComputation(
    string GroupName,
    decimal RemainingAmount);
