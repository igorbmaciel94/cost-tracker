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
            GroupName = "Custos Fixos",
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
    public void ComputeGroupMetrics_ShouldCalculateAmountsPercentagesAndStatus()
    {
        var essenciais = new CategoryBudget
        {
            Id = Guid.NewGuid(),
            Name = "Arrendamento",
            GroupName = "Custos Fixos",
            PlannedAmount = 600m,
            DisplayOrder = 1
        };

        var desejos = new CategoryBudget
        {
            Id = Guid.NewGuid(),
            Name = "Lazer",
            GroupName = "Prazeres",
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
            new() { GroupName = "Custos Fixos", TargetPercent = 0.6m },
            new() { GroupName = "Prazeres", TargetPercent = 0.4m }
        };

        var metrics = MonthCalculations.ComputeGroupMetrics([essenciais, desejos], entries, targets)
            .ToDictionary(x => x.GroupName, x => x);

        Assert.Equal(0.6m, metrics["Custos Fixos"].CurrentPlannedPercent);
        Assert.Equal(0.75m, metrics["Custos Fixos"].CurrentSpentPercent);
        Assert.Equal(600m, metrics["Custos Fixos"].PlannedAmount);
        Assert.Equal(300m, metrics["Custos Fixos"].SpentAmount);
        Assert.Equal("OK", metrics["Custos Fixos"].PlannedStatus);
        Assert.Equal(-300m, metrics["Custos Fixos"].SpentDifference);
        Assert.Equal("Abaixo", metrics["Custos Fixos"].SpentStatus);
        Assert.Equal("Abaixo", metrics["Prazeres"].SpentStatus);
    }

    [Fact]
    public void ComputeRemainingByGroup_ShouldSumRemainingBalancePerGroup()
    {
        var mercado = new CategoryBudget
        {
            Id = Guid.NewGuid(),
            Name = "Mercado",
            GroupName = "Custos Fixos",
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
            GroupName = "Custos Fixos",
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

        Assert.Equal(325m, remainingByGroup["Custos Fixos"]);
        Assert.Equal(-50m, remainingByGroup["Liberdade Financeira"]);
    }

    [Fact]
    public void ComputeAvailableBalanceByCategory_ShouldSubtractOverflowsFromLazerBeforeComprasOnlineAndSaving()
    {
        var saving = new CategoryBudget
        {
            Id = Guid.NewGuid(),
            Name = "Saving",
            GroupName = "Liberdade Financeira",
            PlannedAmount = 552m,
            DisplayOrder = 1
        };

        var credito = new CategoryBudget
        {
            Id = Guid.NewGuid(),
            Name = "Credito",
            GroupName = "Custos Fixos",
            PlannedAmount = 75m,
            DisplayOrder = 2
        };

        var lazer = new CategoryBudget
        {
            Id = Guid.NewGuid(),
            Name = "Lazer",
            GroupName = "Prazeres",
            PlannedAmount = 315m,
            DisplayOrder = 3
        };

        var comprasOnline = new CategoryBudget
        {
            Id = Guid.NewGuid(),
            Name = "Compras online",
            GroupName = "Prazeres",
            PlannedAmount = 172m,
            DisplayOrder = 4
        };

        var mercado = new CategoryBudget
        {
            Id = Guid.NewGuid(),
            Name = "Mercado",
            GroupName = "Custos Fixos",
            PlannedAmount = 100m,
            DisplayOrder = 5
        };

        var entries = new List<Entry>
        {
            new() { CategoryBudgetId = credito.Id, Amount = 425m }
        };

        var available = MonthCalculations.ComputeAvailableBalanceByCategory(
                [saving, credito, lazer, comprasOnline, mercado],
                entries)
            .ToDictionary(x => x.CategoryName, x => x.RemainingAmount);

        Assert.Equal(552m, available["Saving"]);
        Assert.Equal(0m, available["Credito"]);
        Assert.Equal(0m, available["Lazer"]);
        Assert.Equal(137m, available["Compras online"]);
        Assert.Equal(100m, available["Mercado"]);
        Assert.Equal(789m, available.Values.Sum());
    }

    [Fact]
    public void ComputeAvailableBalanceByCategory_ShouldUseSavingOnlyAfterLazerAndComprasOnlineReachZero()
    {
        var saving = new CategoryBudget
        {
            Id = Guid.NewGuid(),
            Name = "Saving",
            GroupName = "Liberdade Financeira",
            PlannedAmount = 552m,
            DisplayOrder = 1
        };

        var credito = new CategoryBudget
        {
            Id = Guid.NewGuid(),
            Name = "Credito",
            GroupName = "Custos Fixos",
            PlannedAmount = 75m,
            DisplayOrder = 2
        };

        var lazer = new CategoryBudget
        {
            Id = Guid.NewGuid(),
            Name = "Lazer",
            GroupName = "Prazeres",
            PlannedAmount = 315m,
            DisplayOrder = 3
        };

        var comprasOnline = new CategoryBudget
        {
            Id = Guid.NewGuid(),
            Name = "Compras online",
            GroupName = "Prazeres",
            PlannedAmount = 172m,
            DisplayOrder = 4
        };

        var mercado = new CategoryBudget
        {
            Id = Guid.NewGuid(),
            Name = "Mercado",
            GroupName = "Custos Fixos",
            PlannedAmount = 100m,
            DisplayOrder = 5
        };

        var entries = new List<Entry>
        {
            new() { CategoryBudgetId = credito.Id, Amount = 650m }
        };

        var available = MonthCalculations.ComputeAvailableBalanceByCategory(
                [saving, credito, lazer, comprasOnline, mercado],
                entries)
            .ToDictionary(x => x.CategoryName, x => x.RemainingAmount);

        Assert.Equal(464m, available["Saving"]);
        Assert.Equal(0m, available["Credito"]);
        Assert.Equal(0m, available["Lazer"]);
        Assert.Equal(0m, available["Compras online"]);
        Assert.Equal(100m, available["Mercado"]);
        Assert.Equal(564m, available.Values.Sum());
    }

    [Fact]
    public void ComputeAvailableBalanceByGroup_ShouldUseAdjustedCategoryBalances()
    {
        var saving = new CategoryBudget
        {
            Id = Guid.NewGuid(),
            Name = "Saving",
            GroupName = "Liberdade Financeira",
            PlannedAmount = 552m,
            DisplayOrder = 1
        };

        var credito = new CategoryBudget
        {
            Id = Guid.NewGuid(),
            Name = "Credito",
            GroupName = "Custos Fixos",
            PlannedAmount = 75m,
            DisplayOrder = 2
        };

        var comprasOnline = new CategoryBudget
        {
            Id = Guid.NewGuid(),
            Name = "Compras online",
            GroupName = "Prazeres",
            PlannedAmount = 172m,
            DisplayOrder = 3
        };

        var entries = new List<Entry>
        {
            new() { CategoryBudgetId = credito.Id, Amount = 384m },
            new() { CategoryBudgetId = comprasOnline.Id, Amount = 305m }
        };

        var availableByGroup = MonthCalculations.ComputeAvailableBalanceByGroup(
                [saving, credito, comprasOnline],
                entries)
            .ToDictionary(x => x.GroupName, x => x.RemainingAmount);

        Assert.Equal(110m, availableByGroup["Liberdade Financeira"]);
        Assert.Equal(0m, availableByGroup["Custos Fixos"]);
        Assert.Equal(0m, availableByGroup["Prazeres"]);
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
