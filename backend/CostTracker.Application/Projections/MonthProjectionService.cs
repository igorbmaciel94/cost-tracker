using System.Globalization;
using CostTracker.Application.Contracts;
using CostTracker.Domain.Calculations;
using CostTracker.Domain.Entities;
using CostTracker.Domain.Enums;

namespace CostTracker.Application.Projections;

public class MonthProjectionService
{
    public MonthSummaryDto ToMonthSummary(Month month)
    {
        var plannedTotal = MonthCalculations.ComputePlannedTotal(month.CategoryBudgets);
        var spentTotal = MonthCalculations.ComputeSpentTotal(month.Entries);

        return new MonthSummaryDto(
            month.Id,
            month.ReferenceMonth,
            month.Salary,
            month.Currency,
            ToStatusString(month.Status),
            plannedTotal,
            spentTotal,
            plannedTotal - spentTotal,
            plannedTotal > month.Salary,
            spentTotal > month.Salary);
    }

    public BudgetResponseDto ToBudgetResponse(Month month)
    {
        var lines = MonthCalculations
            .ComputeBudgetLines(month.CategoryBudgets, month.Entries)
            .Select(item => new BudgetLineDto(
                item.CategoryId,
                item.CategoryName,
                item.GroupName,
                item.Planned,
                item.Spent,
                item.Difference,
                item.DisplayOrder))
            .ToList();

        var plannedTotal = lines.Sum(x => x.Planned);
        var spentTotal = lines.Sum(x => x.Spent);

        return new BudgetResponseDto(
            month.Id,
            month.ReferenceMonth,
            month.Salary,
            plannedTotal,
            spentTotal,
            plannedTotal - spentTotal,
            lines);
    }

    public EntriesResponseDto ToEntriesResponse(Month month)
    {
        var categoryById = month.CategoryBudgets.ToDictionary(x => x.Id, x => x.Name);

        var items = month.Entries
            .OrderByDescending(x => x.EntryDate)
            .ThenBy(x => x.Description)
            .Select(entry => new EntryDto(
                entry.Id,
                entry.CategoryBudgetId,
                categoryById.GetValueOrDefault(entry.CategoryBudgetId, string.Empty),
                entry.EntryDate,
                entry.Description,
                entry.Amount))
            .ToList();

        return new EntriesResponseDto(
            month.Id,
            month.ReferenceMonth,
            items.Sum(x => x.Amount),
            items);
    }

    public TargetsResponseDto ToTargetsResponse(Month month)
    {
        var items = MonthCalculations
            .ComputeGroupMetrics(month.CategoryBudgets, month.Entries, month.GroupTargets)
            .Select(metric => new TargetGroupDto(
                metric.GroupName,
                metric.TargetPercent,
                metric.CurrentPlannedPercent,
                metric.CurrentSpentPercent,
                metric.PlannedDifference,
                metric.PlannedStatus,
                metric.SpentDifference,
                metric.SpentStatus))
            .ToList();

        return new TargetsResponseDto(month.Id, month.ReferenceMonth, items);
    }

    public DashboardDto ToDashboard(Month month)
    {
        var budget = MonthCalculations.ComputeBudgetLines(month.CategoryBudgets, month.Entries);
        var availableBalances = MonthCalculations.ComputeAvailableBalanceByCategory(month.CategoryBudgets, month.Entries);
        var plannedTotal = budget.Sum(x => x.Planned);
        var spentTotal = budget.Sum(x => x.Spent);

        var categoryChart = availableBalances
            .Where(line => line.RemainingAmount > 0)
            .Select(line => new DashboardCategoryPointDto(line.CategoryName, line.RemainingAmount))
            .ToList();

        var groupPie = MonthCalculations
            .ComputeAvailableBalanceByGroup(month.CategoryBudgets, month.Entries)
            .Where(group => group.RemainingAmount > 0)
            .Select(group => new DashboardGroupPointDto(group.GroupName, group.RemainingAmount))
            .ToList();

        return new DashboardDto(
            month.Id,
            month.ReferenceMonth,
            month.Salary,
            plannedTotal,
            spentTotal,
            plannedTotal > month.Salary,
            spentTotal > month.Salary,
            categoryChart,
            groupPie);
    }

    public static bool IsOpen(Month month) => month.Status == MonthStatus.Open;

    public static string ToStatusString(MonthStatus status)
    {
        return status == MonthStatus.Open ? "OPEN" : "CLOSED";
    }

    public static bool TryNormalizeReferenceMonth(string? value, out string normalized)
    {
        normalized = string.Empty;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (!DateTime.TryParseExact(value, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return false;
        }

        normalized = parsed.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        return true;
    }

    public static string GetNextReferenceMonth(string currentReferenceMonth)
    {
        if (!TryNormalizeReferenceMonth(currentReferenceMonth, out var normalized))
        {
            throw new InvalidOperationException("Invalid reference month format.");
        }

        var current = DateTime.ParseExact(normalized, "yyyy-MM", CultureInfo.InvariantCulture);
        return current.AddMonths(1).ToString("yyyy-MM", CultureInfo.InvariantCulture);
    }
}
