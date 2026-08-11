using CostTracker.Domain.Enums;
using CostTracker.Domain.ValueObjects;

namespace CostTracker.Tests.Investments;

public class AllocationTargetSetTests
{
    [Fact]
    public void Create_ShouldAcceptEachClassExactlyOnceAtOneHundredPercent()
    {
        var result = AllocationTargetSet.Create(
        [
            new AllocationWeight(AssetClass.Stocks, 0.40m),
            new AllocationWeight(AssetClass.Reits, 0.10m),
            new AllocationWeight(AssetClass.BrazilFixedIncome, 0.30m),
            new AllocationWeight(AssetClass.InternationalFixedIncome, 0.20m)
        ]);

        Assert.Equal(1m, result.Weights.Values.Sum());
        Assert.Equal(0.40m, result.Weights[AssetClass.Stocks]);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("below")]
    [InlineData("above")]
    public void Create_ShouldRejectInvalidTargetSets(string scenario)
    {
        var values = scenario switch
        {
            "missing" => new[]
            {
                new AllocationWeight(AssetClass.Stocks, 0.40m),
                new AllocationWeight(AssetClass.Reits, 0.10m),
                new AllocationWeight(AssetClass.BrazilFixedIncome, 0.50m)
            },
            "duplicate" => new[]
            {
                new AllocationWeight(AssetClass.Stocks, 0.40m),
                new AllocationWeight(AssetClass.Stocks, 0.10m),
                new AllocationWeight(AssetClass.BrazilFixedIncome, 0.30m),
                new AllocationWeight(AssetClass.InternationalFixedIncome, 0.20m)
            },
            "below" => new[]
            {
                new AllocationWeight(AssetClass.Stocks, 0.39m),
                new AllocationWeight(AssetClass.Reits, 0.10m),
                new AllocationWeight(AssetClass.BrazilFixedIncome, 0.30m),
                new AllocationWeight(AssetClass.InternationalFixedIncome, 0.20m)
            },
            _ => new[]
            {
                new AllocationWeight(AssetClass.Stocks, 0.41m),
                new AllocationWeight(AssetClass.Reits, 0.10m),
                new AllocationWeight(AssetClass.BrazilFixedIncome, 0.30m),
                new AllocationWeight(AssetClass.InternationalFixedIncome, 0.20m)
            }
        };

        Assert.Throws<ArgumentException>(() => AllocationTargetSet.Create(values));
    }
}
