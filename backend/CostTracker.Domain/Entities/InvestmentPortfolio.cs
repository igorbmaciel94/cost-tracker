using CostTracker.Domain.ValueObjects;

namespace CostTracker.Domain.Entities;

public class InvestmentPortfolio
{
    public Guid Id { get; set; }
    public byte SingletonKey { get; set; } = 1;
    public CurrencyCode BaseCurrency { get; set; } = CurrencyCode.Eur;
    public long Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<AllocationTarget> AllocationTargets { get; set; } = new List<AllocationTarget>();
    public ICollection<InvestmentInstrument> Instruments { get; set; } = new List<InvestmentInstrument>();

    public static InvestmentPortfolio Create(DateTimeOffset now)
        => new()
        {
            Id = Guid.NewGuid(),
            BaseCurrency = CurrencyCode.Eur,
            CreatedAt = now,
            UpdatedAt = now
        };

    public void ConfigureTargets(AllocationTargetSet targetSet, DateTimeOffset now)
    {
        foreach (var (assetClass, weight) in targetSet.Weights)
        {
            var target = AllocationTargets.SingleOrDefault(item => item.AssetClass == assetClass);
            if (target is null)
            {
                AllocationTargets.Add(new AllocationTarget
                {
                    Id = Guid.NewGuid(),
                    PortfolioId = Id,
                    AssetClass = assetClass,
                    Weight = weight
                });
            }
            else
            {
                target.Weight = weight;
            }
        }

        Touch(now);
    }

    public void Touch(DateTimeOffset now)
    {
        Version++;
        UpdatedAt = now;
    }
}
