namespace CostTracker.Domain.Entities;

public class GroupTarget
{
    public Guid Id { get; set; }
    public Guid MonthId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public decimal TargetPercent { get; set; }

    public Month Month { get; set; } = null!;
}
