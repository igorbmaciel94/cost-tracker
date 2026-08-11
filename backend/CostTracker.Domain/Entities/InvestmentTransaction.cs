using CostTracker.Domain.Enums;
using CostTracker.Domain.ValueObjects;

namespace CostTracker.Domain.Entities;

public class InvestmentTransaction
{
    public Guid Id { get; set; }
    public Guid InstrumentId { get; set; }
    public InvestmentTransactionType Type { get; set; }
    public DateOnly TransactionDate { get; set; }
    public decimal? Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? Amount { get; set; }
    public CurrencyCode Currency { get; set; } = CurrencyCode.Eur;
    public decimal FeeAmount { get; set; }
    public decimal? CurrencyPerEurRate { get; set; }
    public string? Notes { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public InvestmentInstrument Instrument { get; set; } = null!;
}
