namespace CostTracker.Domain.Entities;

public class HealthProfile
{
    public Guid Id { get; set; }
    public decimal EssentialExpenses { get; set; }
    public decimal SavedEmergencyFund { get; set; }
}
