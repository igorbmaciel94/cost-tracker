using CostTracker.Domain.Enums;
using CostTracker.Domain.Investments.Calculations;

namespace CostTracker.Tests.Investments;

public class ContributionAllocatorTests
{
    [Fact]
    public void Calculate_ProjectsTheFrozenTenThousandEuroExampleWithoutSales()
    {
        var portfolio = Snapshot(
            stocks: 3_500m,
            reits: 700m,
            brazilFixedIncome: 3_800m,
            internationalFixedIncome: 2_000m,
            stockInstruments: [Instrument("stock", "XNYS", "STOCK", 3_500m, 1m)],
            reitInstruments: [Instrument("reit", "XNYS", "REIT", 700m, 1m)]);
        var policy = Policy(
            stocks: .40m,
            reits: .10m,
            brazilFixedIncome: .30m,
            internationalFixedIncome: .20m,
            scores: [new("stock", 1), new("reit", 1)]);

        var plan = ContributionAllocator.Calculate(
            portfolio,
            new ContributionAmount(1_000m),
            policy,
            Constraints(.000001m));

        AssertClose(733.333333m, ClassLine(plan, AssetClass.Stocks).PlannedContributionEur);
        AssertClose(233.333333m, ClassLine(plan, AssetClass.Reits).PlannedContributionEur);
        Assert.Equal(0m, ClassLine(plan, AssetClass.BrazilFixedIncome).PlannedContributionEur);
        AssertClose(33.333333m, ClassLine(plan, AssetClass.InternationalFixedIncome).PlannedContributionEur);
        Assert.Equal(1_000m, plan.ClassLines.Sum(x => x.PlannedContributionEur));
        Assert.All(plan.ClassLines, line => Assert.True(line.PlannedContributionEur >= 0m));
        AssertClose(
            ClassLine(plan, AssetClass.InternationalFixedIncome).PlannedContributionEur,
            ClassLine(plan, AssetClass.InternationalFixedIncome).RecommendedContributionEur);
        Assert.DoesNotContain(
            plan.InstrumentLines,
            line => line.AssetClass is AssetClass.BrazilFixedIncome or AssetClass.InternationalFixedIncome);
        Assert.Equal(
            ContributionExplanationCode.ClassReceivesNoContribution,
            ClassLine(plan, AssetClass.BrazilFixedIncome).Explanations[0].Code);
    }

    [Fact]
    public void Calculate_UsesPositiveScoresAsRelativeWeightsAndExcludesScoreZero()
    {
        var portfolio = Snapshot(
            stockInstruments:
            [
                Instrument("three", "XNAS", "THREE", 0m, 1m),
                Instrument("one", "XNAS", "ONE", 0m, 1m),
                Instrument("zero", "XNAS", "ZERO", 0m, 1m)
            ]);
        var policy = Policy(
            stocks: 1m,
            scores: [new("three", 3), new("one", 1), new("zero", 0)]);

        var plan = ContributionAllocator.Calculate(
            portfolio,
            new ContributionAmount(400m),
            policy,
            Constraints(.01m));

        Assert.Equal(300m, InstrumentLine(plan, "three").PlannedContributionEur);
        Assert.Equal(100m, InstrumentLine(plan, "one").PlannedContributionEur);
        Assert.Equal(0m, InstrumentLine(plan, "zero").PlannedContributionEur);
        Assert.Equal(0m, InstrumentLine(plan, "zero").RecommendedContributionEur);
        Assert.Contains(
            InstrumentLine(plan, "zero").Explanations,
            x => x.Code == ContributionExplanationCode.ScoreZeroExcluded);
    }

    [Fact]
    public void Calculate_APriceFallIncreasesTheGapButNominallyCheapSharesDoNotWin()
    {
        var balanced = Snapshot(
            stocks: 100m,
            stockInstruments:
            [
                Instrument("cheap", "XNAS", "CHEAP", 50m, 1m),
                Instrument("expensive", "XNYS", "EXPENSIVE", 50m, 100m)
            ]);
        var afterFall = Snapshot(
            stocks: 70m,
            stockInstruments:
            [
                Instrument("cheap", "XNAS", "CHEAP", 50m, 1m),
                Instrument("expensive", "XNYS", "EXPENSIVE", 20m, 100m)
            ]);
        var policy = Policy(stocks: 1m, scores: [new("cheap", 1), new("expensive", 1)]);
        var constraints = Constraints(.01m);

        var balancedPlan = ContributionAllocator.Calculate(
            balanced,
            new ContributionAmount(20m),
            policy,
            constraints);
        var fallenPlan = ContributionAllocator.Calculate(
            afterFall,
            new ContributionAmount(20m),
            policy,
            constraints);

        Assert.Equal(10m, InstrumentLine(balancedPlan, "expensive").PlannedContributionEur);
        Assert.Equal(20m, InstrumentLine(fallenPlan, "expensive").PlannedContributionEur);
        Assert.Equal(0m, InstrumentLine(fallenPlan, "cheap").PlannedContributionEur);
        Assert.Equal(100m, InstrumentLine(fallenPlan, "expensive").RecommendedAmountNative /
                           InstrumentLine(fallenPlan, "expensive").SuggestedQuantity);
    }

    [Fact]
    public void Calculate_LeavesTheClassAllocationAsResidualWhenNoInstrumentIsEligible()
    {
        var portfolio = Snapshot(
            stockInstruments: [Instrument("excluded", "XNAS", "ZERO", 0m, 0m)]);
        var policy = Policy(stocks: 1m, scores: [new("excluded", 0)]);

        var plan = ContributionAllocator.Calculate(
            portfolio,
            new ContributionAmount(10m),
            policy,
            Constraints(.000001m));

        var stocks = ClassLine(plan, AssetClass.Stocks);
        Assert.Equal(10m, stocks.PlannedContributionEur);
        Assert.Equal(0m, stocks.RecommendedContributionEur);
        Assert.Equal(10m, stocks.ResidualEur);
        Assert.Equal(10m, plan.ResidualEur);
        Assert.Contains(
            stocks.Explanations,
            x => x.Code == ContributionExplanationCode.NoEligibleInstrument);
    }

    [Fact]
    public void Calculate_FloorsQuantityToItsStepAndReportsUnusableResidual()
    {
        var portfolio = Snapshot(
            stockInstruments: [Instrument("stock", "XNAS", "STEP", 0m, 3m)]);
        var policy = Policy(stocks: 1m, scores: [new("stock", 1)]);

        var plan = ContributionAllocator.Calculate(
            portfolio,
            new ContributionAmount(10m),
            policy,
            Constraints(1m));

        var line = InstrumentLine(plan, "stock");
        Assert.Equal(3m, line.SuggestedQuantity);
        Assert.Equal(9m, line.RecommendedContributionEur);
        Assert.Equal(1m, plan.ResidualEur);
        Assert.Contains(
            line.Explanations,
            x => x.Code == ContributionExplanationCode.RoundedDownToQuantityStep);
    }

    [Fact]
    public void Calculate_ReinvestsExecutableResidualIntoTheLargestRemainingGap()
    {
        var portfolio = Snapshot(
            stockInstruments:
            [
                Instrument("four", "XNAS", "FOUR", 0m, 4m),
                Instrument("three", "XNYS", "THREE", 0m, 3m)
            ]);
        var policy = Policy(stocks: 1m, scores: [new("four", 1), new("three", 1)]);

        var plan = ContributionAllocator.Calculate(
            portfolio,
            new ContributionAmount(10m),
            policy,
            Constraints(1m));

        Assert.Equal(4m, InstrumentLine(plan, "four").RecommendedContributionEur);
        Assert.Equal(6m, InstrumentLine(plan, "three").RecommendedContributionEur);
        Assert.Equal(0m, plan.ResidualEur);
        Assert.Contains(
            InstrumentLine(plan, "three").Explanations,
            x => x.Code == ContributionExplanationCode.ResidualReinvested);
    }

    [Fact]
    public void Calculate_UsesMicSymbolAndIdAsStableTieBreakersRegardlessOfInputOrder()
    {
        var instruments = new[]
        {
            Instrument("mic-later", "XNYS", "AAA", 0m, 1m),
            Instrument("symbol-later", "XNAS", "BBB", 0m, 1m),
            Instrument("z-id", "XNAS", "AAA", 0m, 1m),
            Instrument("a-id", "XNAS", "AAA", 0m, 1m)
        };
        var policy = Policy(
            stocks: 1m,
            scores:
            [
                new("mic-later", 1),
                new("symbol-later", 1),
                new("z-id", 1),
                new("a-id", 1)
            ]);

        var first = ContributionAllocator.Calculate(
            Snapshot(stockInstruments: instruments),
            new ContributionAmount(1m),
            policy,
            Constraints(1m));
        var reversed = ContributionAllocator.Calculate(
            Snapshot(stockInstruments: instruments.Reverse().ToArray()),
            new ContributionAmount(1m),
            policy,
            Constraints(1m));

        Assert.Equal(1m, InstrumentLine(first, "a-id").RecommendedContributionEur);
        Assert.Equal(0m, InstrumentLine(first, "z-id").RecommendedContributionEur);
        Assert.Equal(0m, InstrumentLine(first, "symbol-later").RecommendedContributionEur);
        Assert.Equal(0m, InstrumentLine(first, "mic-later").RecommendedContributionEur);
        Assert.Equal(
            first.InstrumentLines.Select(x => (x.InstrumentId, x.RecommendedContributionEur)),
            reversed.InstrumentLines.Select(x => (x.InstrumentId, x.RecommendedContributionEur)));
    }

    [Fact]
    public void Calculate_UsesAnInstrumentSpecificStepOverride()
    {
        var portfolio = Snapshot(
            stockInstruments: [Instrument("stock", "XNAS", "OVERRIDE", 0m, 1m)]);
        var policy = Policy(stocks: 1m, scores: [new("stock", 1)]);

        var plan = ContributionAllocator.Calculate(
            portfolio,
            new ContributionAmount(.75m),
            policy,
            Constraints(1m, [new("stock", .25m)]));

        var line = InstrumentLine(plan, "stock");
        Assert.Equal(.25m, line.QuantityStep);
        Assert.Equal(.75m, line.SuggestedQuantity);
        Assert.Equal(.75m, line.RecommendedContributionEur);
        Assert.Equal(0m, plan.ResidualEur);
    }

    [Fact]
    public void Calculate_ConservesEveryCentAcrossClassPlansInstrumentStepsAndResidual()
    {
        var portfolio = Snapshot(
            stocks: 317.42m,
            reits: 89.17m,
            brazilFixedIncome: 220m,
            internationalFixedIncome: 155m,
            stockInstruments:
            [
                Instrument("s1", "XNAS", "S1", 200m, 43.17m, 1.16m),
                Instrument("s2", "XNYS", "S2", 117.42m, 91.03m, 1.16m)
            ],
            reitInstruments: [Instrument("r1", "XNYS", "R1", 89.17m, 52.11m, 1.16m)]);
        var policy = Policy(
            stocks: .45m,
            reits: .15m,
            brazilFixedIncome: .25m,
            internationalFixedIncome: .15m,
            scores: [new("s1", 2), new("s2", 1), new("r1", 1)]);

        var plan = ContributionAllocator.Calculate(
            portfolio,
            new ContributionAmount(237.89m),
            policy,
            Constraints(.001m));

        Assert.Equal(plan.AvailableAmountEur, plan.TotalRecommendedEur + plan.ResidualEur);
        AssertClose(plan.ResidualEur, plan.ClassLines.Sum(x => x.ResidualEur));
        Assert.True(plan.TotalRecommendedEur <= plan.AvailableAmountEur);
        Assert.All(plan.ClassLines, line => Assert.True(line.RecommendedContributionEur >= 0m));

        var marketRecommended = plan.ClassLines
            .Where(x => x.AssetClass is AssetClass.Stocks or AssetClass.Reits)
            .Sum(x => x.RecommendedContributionEur);
        Assert.Equal(marketRecommended, plan.InstrumentLines.Sum(x => x.RecommendedContributionEur));
    }

    [Fact]
    public void Calculate_RejectsPoliciesThatDoNotTotalOneHundredPercent()
    {
        var portfolio = Snapshot();
        var invalidPolicy = Policy(stocks: .4m, reits: .1m, brazilFixedIncome: .3m, internationalFixedIncome: .1m);

        var exception = Assert.Throws<ArgumentException>(() => ContributionAllocator.Calculate(
            portfolio,
            new ContributionAmount(100m),
            invalidPolicy,
            Constraints(.000001m)));

        Assert.Contains("sum exactly to one", exception.Message);
    }

    private static PortfolioSnapshot Snapshot(
        decimal stocks = 0m,
        decimal reits = 0m,
        decimal brazilFixedIncome = 0m,
        decimal internationalFixedIncome = 0m,
        IReadOnlyList<InstrumentSnapshot>? stockInstruments = null,
        IReadOnlyList<InstrumentSnapshot>? reitInstruments = null)
    {
        return new PortfolioSnapshot(
            "portfolio-v1",
            [
                new(AssetClass.Stocks, stocks, stockInstruments ?? []),
                new(AssetClass.Reits, reits, reitInstruments ?? []),
                new(AssetClass.BrazilFixedIncome, brazilFixedIncome, []),
                new(AssetClass.InternationalFixedIncome, internationalFixedIncome, [])
            ]);
    }

    private static AllocationPolicy Policy(
        decimal stocks = 0m,
        decimal reits = 0m,
        decimal brazilFixedIncome = 0m,
        decimal internationalFixedIncome = 0m,
        IReadOnlyList<InstrumentAllocationScore>? scores = null)
    {
        return new AllocationPolicy(
            "policy-v1",
            [
                new(AssetClass.Stocks, stocks),
                new(AssetClass.Reits, reits),
                new(AssetClass.BrazilFixedIncome, brazilFixedIncome),
                new(AssetClass.InternationalFixedIncome, internationalFixedIncome)
            ],
            scores ?? []);
    }

    private static ExecutionConstraints Constraints(
        decimal defaultStep,
        IReadOnlyList<InstrumentExecutionConstraint>? overrides = null)
    {
        return new ExecutionConstraints(defaultStep, overrides ?? []);
    }

    private static InstrumentSnapshot Instrument(
        string id,
        string mic,
        string symbol,
        decimal valueEur,
        decimal priceNative,
        decimal nativeCurrencyPerEur = 1m)
    {
        return new InstrumentSnapshot(id, mic, symbol, valueEur, priceNative, nativeCurrencyPerEur);
    }

    private static ClassContributionPlanLine ClassLine(ContributionPlan plan, AssetClass assetClass)
    {
        return Assert.Single(plan.ClassLines, x => x.AssetClass == assetClass);
    }

    private static InstrumentContributionPlanLine InstrumentLine(ContributionPlan plan, string instrumentId)
    {
        return Assert.Single(plan.InstrumentLines, x => x.InstrumentId == instrumentId);
    }

    private static void AssertClose(decimal expected, decimal actual)
    {
        Assert.InRange(actual, expected - .000001m, expected + .000001m);
    }
}
