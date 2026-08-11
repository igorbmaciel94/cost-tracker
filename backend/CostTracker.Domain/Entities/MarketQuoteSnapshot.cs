using CostTracker.Domain.ValueObjects;

namespace CostTracker.Domain.Entities;

public class MarketQuoteSnapshot
{
    public Guid Id { get; set; }
    public Guid InstrumentId { get; set; }
    public string ProviderCode { get; set; } = string.Empty;
    public string ProviderSymbol { get; set; } = string.Empty;
    public string? Exchange { get; set; }
    public string? Mic { get; set; }
    public decimal Price { get; set; }
    public CurrencyCode Currency { get; set; } = CurrencyCode.Eur;
    public string PriceKind { get; set; } = "EOD_CLOSE";
    public DateOnly AsOf { get; set; }
    public DateTimeOffset FetchedAt { get; set; }
    public bool IsFallback { get; set; }
    public string RawPayloadHash { get; set; } = string.Empty;

    public InvestmentInstrument Instrument { get; set; } = null!;
}
