using System.Text.Json;
using CostTracker.Domain.Entities;
using CostTracker.Domain.Enums;
using CostTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CostTracker.Infrastructure.Seed;

public class DatabaseSeeder(
    CostTrackerDbContext dbContext,
    ILogger<DatabaseSeeder> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var hasAnyMonth = await dbContext.Months.AnyAsync(cancellationToken);
        if (hasAnyMonth)
        {
            return;
        }

        var seedModel = await LoadSeedAsync(cancellationToken);
        var monthId = Guid.NewGuid();

        var month = new Month
        {
            Id = monthId,
            ReferenceMonth = seedModel.ReferenceMonth,
            Salary = seedModel.Salary,
            Currency = seedModel.Currency,
            Status = MonthStatus.Open,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var categories = seedModel.Categories
            .Select(item => new CategoryBudget
            {
                Id = Guid.NewGuid(),
                MonthId = monthId,
                Name = item.Name,
                GroupName = item.GroupName,
                PlannedAmount = item.PlannedAmount,
                DisplayOrder = item.DisplayOrder
            })
            .ToList();

        var categoryByName = categories
            .ToDictionary(x => x.Name, x => x.Id, StringComparer.OrdinalIgnoreCase);

        var targets = seedModel.Targets
            .Select(item => new GroupTarget
            {
                Id = Guid.NewGuid(),
                MonthId = monthId,
                GroupName = item.GroupName,
                TargetPercent = item.TargetPercent
            })
            .ToList();

        var entries = seedModel.Entries
            .Where(item => categoryByName.ContainsKey(item.CategoryName))
            .Select(item => new Entry
            {
                Id = Guid.NewGuid(),
                MonthId = monthId,
                CategoryBudgetId = categoryByName[item.CategoryName],
                EntryDate = item.EntryDate,
                Description = item.Description,
                Amount = item.Amount,
                CreatedAt = DateTimeOffset.UtcNow
            })
            .ToList();

        await dbContext.Months.AddAsync(month, cancellationToken);
        await dbContext.CategoryBudgets.AddRangeAsync(categories, cancellationToken);
        await dbContext.GroupTargets.AddRangeAsync(targets, cancellationToken);
        await dbContext.Entries.AddRangeAsync(entries, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Initial month seed applied with reference month {ReferenceMonth}.", month.ReferenceMonth);
    }

    private static async Task<InitialMonthSeedModel> LoadSeedAsync(CancellationToken cancellationToken)
    {
        var seedPath = Path.Combine(AppContext.BaseDirectory, "Seed", "initial-month.json");

        if (!File.Exists(seedPath))
        {
            return InitialMonthSeedModel.CreateDefault();
        }

        await using var stream = File.OpenRead(seedPath);
        var model = await JsonSerializer.DeserializeAsync<InitialMonthSeedModel>(stream, JsonOptions, cancellationToken);

        return model ?? InitialMonthSeedModel.CreateDefault();
    }
}
