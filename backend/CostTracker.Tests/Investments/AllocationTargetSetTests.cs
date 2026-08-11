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
            new AllocationWeight(AssetClass.InternationalFixedIncome, 0.15m),
            new AllocationWeight(AssetClass.Cryptocurrencies, 0.05m)
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
                new AllocationWeight(AssetClass.BrazilFixedIncome, 0.30m),
                new AllocationWeight(AssetClass.InternationalFixedIncome, 0.20m)
            },
            "duplicate" => new[]
            {
                new AllocationWeight(AssetClass.Stocks, 0.40m),
                new AllocationWeight(AssetClass.Stocks, 0.10m),
                new AllocationWeight(AssetClass.BrazilFixedIncome, 0.30m),
                new AllocationWeight(AssetClass.InternationalFixedIncome, 0.15m),
                new AllocationWeight(AssetClass.Cryptocurrencies, 0.05m)
            },
            "below" => new[]
            {
                new AllocationWeight(AssetClass.Stocks, 0.39m),
                new AllocationWeight(AssetClass.Reits, 0.10m),
                new AllocationWeight(AssetClass.BrazilFixedIncome, 0.30m),
                new AllocationWeight(AssetClass.InternationalFixedIncome, 0.15m),
                new AllocationWeight(AssetClass.Cryptocurrencies, 0.05m)
            },
            _ => new[]
            {
                new AllocationWeight(AssetClass.Stocks, 0.41m),
                new AllocationWeight(AssetClass.Reits, 0.10m),
                new AllocationWeight(AssetClass.BrazilFixedIncome, 0.30m),
                new AllocationWeight(AssetClass.InternationalFixedIncome, 0.15m),
                new AllocationWeight(AssetClass.Cryptocurrencies, 0.05m)
            }
        };

        Assert.Throws<ArgumentException>(() => AllocationTargetSet.Create(values));
    }

    [Fact]
    public void Create_ShouldRejectFractionalPercentagesEvenWhenTheyTotalOneHundredPercent()
    {
        var exception = Assert.Throws<ArgumentException>(() => AllocationTargetSet.Create(
        [
            new AllocationWeight(AssetClass.Stocks, 0.405m),
            new AllocationWeight(AssetClass.Reits, 0.095m),
            new AllocationWeight(AssetClass.BrazilFixedIncome, 0.30m),
            new AllocationWeight(AssetClass.InternationalFixedIncome, 0.15m),
            new AllocationWeight(AssetClass.Cryptocurrencies, 0.05m)
        ]));

        Assert.Contains("whole percentage", exception.Message);
    }
}
