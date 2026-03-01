namespace CostTracker.Domain.Entities;

public class Entry
{
    public Guid Id { get; set; }
    public Guid MonthId { get; set; }
    public Guid CategoryBudgetId { get; set; }
    public DateOnly EntryDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Month Month { get; set; } = null!;
    public CategoryBudget CategoryBudget { get; set; } = null!;
}
