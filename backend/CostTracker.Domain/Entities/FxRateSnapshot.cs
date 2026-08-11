using CostTracker.Domain.ValueObjects;

namespace CostTracker.Domain.Entities;

public class FxRateSnapshot
{
    public Guid Id { get; set; }
    public string ProviderCode { get; set; } = string.Empty;
    public CurrencyCode BaseCurrency { get; set; } = CurrencyCode.Eur;
    public CurrencyCode QuoteCurrency { get; set; } = CurrencyCode.Eur;
    public decimal Rate { get; set; }
    public string RateKind { get; set; } = string.Empty;
    public DateOnly AsOf { get; set; }
    public DateTimeOffset FetchedAt { get; set; }
    public bool IsFallback { get; set; }
    public string RawPayloadHash { get; set; } = string.Empty;
}
