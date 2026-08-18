using CostTracker.Domain.ValueObjects;

namespace CostTracker.Domain.Entities;

public class DividendEvent
{
    public Guid Id { get; set; }
    public Guid InstrumentId { get; set; }
    public decimal GrossAmountPerUnit { get; set; }
    public decimal WithholdingTaxRate { get; set; }
    public CurrencyCode Currency { get; set; } = CurrencyCode.Eur;
    public DateOnly ExDate { get; set; }
    public DateOnly PaymentDate { get; set; }
    public string? Notes { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public decimal? EligibleQuantity { get; set; }
    public decimal? GrossAmount { get; set; }
    public decimal? WithholdingTaxAmount { get; set; }
    public decimal? NetAmount { get; set; }
    public decimal? CurrencyPerEurRate { get; set; }
    public decimal? NetAmountEur { get; set; }
    public DateOnly? FxAsOf { get; set; }
    public string? FxProviderCode { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public InvestmentInstrument Instrument { get; set; } = null!;
}
