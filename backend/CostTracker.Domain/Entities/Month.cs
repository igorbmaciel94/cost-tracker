using CostTracker.Domain.Enums;

namespace CostTracker.Domain.Entities;

public class Month
{
    public Guid Id { get; set; }
    public string ReferenceMonth { get; set; } = string.Empty;
    public decimal Salary { get; set; }
    public string Currency { get; set; } = "EUR";
    public MonthStatus Status { get; set; } = MonthStatus.Open;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ClosedAt { get; set; }
    public Guid? ClonedFromMonthId { get; set; }

    public ICollection<CategoryBudget> CategoryBudgets { get; set; } = new List<CategoryBudget>();
    public ICollection<Entry> Entries { get; set; } = new List<Entry>();
    public ICollection<GroupTarget> GroupTargets { get; set; } = new List<GroupTarget>();
}
