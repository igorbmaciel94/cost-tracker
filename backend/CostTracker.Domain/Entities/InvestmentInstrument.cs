using CostTracker.Domain.Enums;
using CostTracker.Domain.ValueObjects;

namespace CostTracker.Domain.Entities;

public class InvestmentInstrument
{
    public Guid Id { get; set; }
    public Guid PortfolioId { get; set; }
    public AssetClass AssetClass { get; set; }
    public InstrumentKind Kind { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? PublicIdentifier { get; set; }
    public string? Ticker { get; set; }
    public string? Mic { get; set; }
    public string? Isin { get; set; }
    public string IdentityKey { get; set; } = string.Empty;
    public CurrencyCode NativeCurrency { get; set; } = CurrencyCode.Eur;
    public ValuationMode ValuationMode { get; set; }
    public int AllocationScore { get; set; }
    public decimal? QuantityStep { get; set; }
    public bool IsArchived { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public long Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public InvestmentPortfolio Portfolio { get; set; } = null!;
    public ICollection<InvestmentTransaction> Transactions { get; set; } = new List<InvestmentTransaction>();
    public ICollection<ManualValuation> ManualValuations { get; set; } = new List<ManualValuation>();

    public void Touch(DateTimeOffset now)
    {
        Version++;
        UpdatedAt = now;
    }

    public void Archive(DateTimeOffset now)
    {
        if (IsArchived)
            return;

        IsArchived = true;
        ArchivedAt = now;
        Touch(now);
    }
}
