using CostTracker.Domain.ValueObjects;

namespace CostTracker.Domain.Entities;

public class ManualValuation
{
    public Guid Id { get; set; }
    public Guid InstrumentId { get; set; }
    public decimal Amount { get; set; }
    public CurrencyCode Currency { get; set; } = CurrencyCode.Eur;
    public DateOnly AsOf { get; set; }
    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;
    public string IdempotencyKey { get; set; } = string.Empty;

    public InvestmentInstrument Instrument { get; set; } = null!;
}
