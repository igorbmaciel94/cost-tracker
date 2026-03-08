using CostTracker.Domain.Calculations;
using CostTracker.Domain.Entities;

namespace CostTracker.Tests.Calculations;

public class MonthCalculationsTests
{
    [Fact]
    public void ComputeBudgetLines_ShouldCalculateSpentAndDifference()
    {
        var category = new CategoryBudget
        {
            Id = Guid.NewGuid(),
            Name = "Mercado",
            GroupName = "Essenciais",
            PlannedAmount = 400m,
            DisplayOrder = 1
        };

        var entries = new List<Entry>
        {
            new() { CategoryBudgetId = category.Id, Amount = 100m },
            new() { CategoryBudgetId = category.Id, Amount = 50m }
        };

        var lines = MonthCalculations.ComputeBudgetLines([category], entries);

        Assert.Single(lines);
        Assert.Equal(150m, lines[0].Spent);
        Assert.Equal(250m, lines[0].Difference);
    }

    [Fact]
    public void ComputeGroupMetrics_ShouldCalculatePercentagesAndStatus()
    {
        var essenciais = new CategoryBudget
        {
            Id = Guid.NewGuid(),
            Name = "Arrendamento",
            GroupName = "Essenciais",
            PlannedAmount = 600m,
            DisplayOrder = 1
        };

        var desejos = new CategoryBudget
        {
            Id = Guid.NewGuid(),
            Name = "Lazer",
            GroupName = "Desejos",
            PlannedAmount = 400m,
            DisplayOrder = 2
        };

        var entries = new List<Entry>
        {
            new() { CategoryBudgetId = essenciais.Id, Amount = 300m },
            new() { CategoryBudgetId = desejos.Id, Amount = 100m }
        };

        var targets = new List<GroupTarget>
        {
            new() { GroupName = "Essenciais", TargetPercent = 0.6m },
            new() { GroupName = "Desejos", TargetPercent = 0.4m }
        };

        var metrics = MonthCalculations.ComputeGroupMetrics([essenciais, desejos], entries, targets)
            .ToDictionary(x => x.GroupName, x => x);

        Assert.Equal(0.6m, metrics["Essenciais"].CurrentPlannedPercent);
        Assert.Equal(0.75m, metrics["Essenciais"].CurrentSpentPercent);
        Assert.Equal("OK", metrics["Essenciais"].PlannedStatus);
        Assert.Equal("Acima", metrics["Essenciais"].SpentStatus);
        Assert.Equal("Abaixo", metrics["Desejos"].SpentStatus);
    }

    [Fact]
    public void ComputeRemainingByGroup_ShouldSumRemainingBalancePerGroup()
    {
        var mercado = new CategoryBudget
        {
            Id = Guid.NewGuid(),
            Name = "Mercado",
            GroupName = "Essenciais",
            PlannedAmount = 400m,
            DisplayOrder = 1
        };

        var renda = new CategoryBudget
        {
            Id = Guid.NewGuid(),
            Name = "Renda Extra",
            GroupName = "Investimentos",
            PlannedAmount = 200m,
            DisplayOrder = 2
        };

        var farmacia = new CategoryBudget
        {
            Id = Guid.NewGuid(),
            Name = "Farmácia",
            GroupName = "Essenciais",
            PlannedAmount = 100m,
            DisplayOrder = 3
        };

        var entries = new List<Entry>
        {
            new() { CategoryBudgetId = mercado.Id, Amount = 150m },
            new() { CategoryBudgetId = renda.Id, Amount = 250m },
            new() { CategoryBudgetId = farmacia.Id, Amount = 25m }
        };

        var remainingByGroup = MonthCalculations.ComputeRemainingByGroup(
                [mercado, renda, farmacia],
                entries)
            .ToDictionary(x => x.GroupName, x => x.RemainingAmount);

        Assert.Equal(325m, remainingByGroup["Essenciais"]);
        Assert.Equal(-50m, remainingByGroup["Investimentos"]);
    }

    [Fact]
    public void ResolveStatus_ShouldReturnOkWithinHalfPoint()
    {
        Assert.Equal("OK", MonthCalculations.ResolveStatus(0.005m));
        Assert.Equal("OK", MonthCalculations.ResolveStatus(-0.005m));
        Assert.Equal("Acima", MonthCalculations.ResolveStatus(0.006m));
        Assert.Equal("Abaixo", MonthCalculations.ResolveStatus(-0.006m));
    }
}
