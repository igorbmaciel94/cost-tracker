namespace CostTracker.Api.Contracts;

public sealed record MonthSummaryDto(
    Guid Id,
    string ReferenceMonth,
    decimal Salary,
    string Currency,
    string Status,
    decimal PlannedTotal,
    decimal SpentTotal,
    decimal DifferenceTotal,
    bool IsOverPlanned,
    bool IsOverSpent);

public sealed record BudgetLineDto(
    Guid Id,
    string Name,
    string GroupName,
    decimal Planned,
    decimal Spent,
    decimal Difference,
    int DisplayOrder);

public sealed record BudgetResponseDto(
    Guid MonthId,
    string ReferenceMonth,
    decimal Salary,
    decimal PlannedTotal,
    decimal SpentTotal,
    decimal DifferenceTotal,
    IReadOnlyList<BudgetLineDto> Lines);

public sealed record EntryDto(
    Guid Id,
    Guid CategoryBudgetId,
    string CategoryName,
    DateOnly EntryDate,
    string Description,
    decimal Amount);

public sealed record EntriesResponseDto(
    Guid MonthId,
    string ReferenceMonth,
    decimal TotalSpent,
    IReadOnlyList<EntryDto> Items);

public sealed record TargetGroupDto(
    string GroupName,
    decimal TargetPercent,
    decimal CurrentPlannedPercent,
    decimal CurrentSpentPercent,
    decimal PlannedDifference,
    string PlannedStatus,
    decimal SpentDifference,
    string SpentStatus);

public sealed record TargetsResponseDto(
    Guid MonthId,
    string ReferenceMonth,
    IReadOnlyList<TargetGroupDto> Items);

public sealed record DashboardCategoryPointDto(
    string Category,
    decimal Remaining);

public sealed record DashboardGroupPointDto(
    string GroupName,
    decimal Remaining);

public sealed record DashboardDto(
    Guid MonthId,
    string ReferenceMonth,
    decimal Salary,
    decimal PlannedTotal,
    decimal SpentTotal,
    bool IsOverPlanned,
    bool IsOverSpent,
    IReadOnlyList<DashboardCategoryPointDto> CategoryChart,
    IReadOnlyList<DashboardGroupPointDto> GroupPie);

public sealed record AuthSessionDto(
    bool IsAuthenticated,
    string? Username);

public sealed class CreateMonthRequest
{
    public string? ReferenceMonth { get; set; }
}

public sealed class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class UpdateSalaryRequest
{
    public decimal Salary { get; set; }
}

public sealed class CreateCategoryRequest
{
    public string Name { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public decimal PlannedAmount { get; set; }
    public int? DisplayOrder { get; set; }
}

public sealed class UpdateCategoryRequest
{
    public string Name { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public decimal PlannedAmount { get; set; }
    public int? DisplayOrder { get; set; }
}

public sealed class CreateEntryRequest
{
    public Guid CategoryBudgetId { get; set; }
    public DateOnly EntryDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public sealed class UpdateEntryRequest
{
    public Guid CategoryBudgetId { get; set; }
    public DateOnly EntryDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public sealed class UpdateTargetsRequest
{
    public IReadOnlyList<UpdateTargetGroupRequest> Items { get; set; } = [];
}

public sealed class UpdateTargetGroupRequest
{
    public string GroupName { get; set; } = string.Empty;
    public decimal TargetPercent { get; set; }
}

public sealed record PlanningGoalDto(
    Guid Id,
    string Name,
    decimal TotalAmount,
    decimal SavedAmount,
    int Months);

public sealed class CreatePlanningGoalRequest
{
    public string Name { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal SavedAmount { get; set; }
    public int Months { get; set; }
}

public sealed class UpdatePlanningGoalRequest
{
    public string Name { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal SavedAmount { get; set; }
    public int Months { get; set; }
}
