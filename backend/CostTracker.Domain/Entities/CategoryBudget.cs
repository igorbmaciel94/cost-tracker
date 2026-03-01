namespace CostTracker.Domain.Entities;

public class CategoryBudget
{
    public Guid Id { get; set; }
    public Guid MonthId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public decimal PlannedAmount { get; set; }
    public int DisplayOrder { get; set; }

    public Month Month { get; set; } = null!;
    public ICollection<Entry> Entries { get; set; } = new List<Entry>();
}
