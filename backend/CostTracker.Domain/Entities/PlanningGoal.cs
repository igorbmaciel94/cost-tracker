namespace CostTracker.Domain.Entities;

public class PlanningGoal
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal SavedAmount { get; set; }
    public int Months { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
