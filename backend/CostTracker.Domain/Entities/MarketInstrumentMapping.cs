using CostTracker.Domain.ValueObjects;

namespace CostTracker.Domain.Entities;

public class MarketInstrumentMapping
{
    public Guid Id { get; set; }
    public Guid InstrumentId { get; set; }
    public string ProviderCode { get; set; } = string.Empty;
    public string ProviderSymbol { get; set; } = string.Empty;
    public string? Exchange { get; set; }
    public string? Mic { get; set; }
    public CurrencyCode QuoteCurrency { get; set; } = CurrencyCode.Eur;
    public decimal PriceMultiplier { get; set; } = 1m;
    public bool IsEnabled { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public InvestmentInstrument Instrument { get; set; } = null!;
}
