using CostTracker.Application.Contracts;
using CostTracker.Application.Exceptions;
using CostTracker.Application.Interfaces;
using CostTracker.Application.Projections;
using CostTracker.Domain.Constants;
using CostTracker.Domain.Entities;
using CostTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CostTracker.Application.Services;

public class MonthService(ICostTrackerDbContext dbContext, MonthProjectionService projectionService)
{
    public async Task<IReadOnlyList<MonthSummaryDto>> GetMonthsAsync(CancellationToken cancellationToken)
    {
        var months = await dbContext.Months
            .WithDetails()
            .OrderByDescending(x => x.ReferenceMonth)
            .ToListAsync(cancellationToken);

        return months.Select(projectionService.ToMonthSummary).ToList();
    }

    public async Task<MonthSummaryDto> CreateNewMonthAsync(string? desiredReferenceMonth, CancellationToken cancellationToken)
    {
        var openMonth = await dbContext.Months
            .WithDetails()
            .FirstOrDefaultAsync(x => x.Status == MonthStatus.Open, cancellationToken);

        if (openMonth is null)
            throw new NotFoundException("No OPEN month found to clone.");

        string nextReferenceMonth;
        if (string.IsNullOrWhiteSpace(desiredReferenceMonth))
        {
            nextReferenceMonth = MonthProjectionService.GetNextReferenceMonth(openMonth.ReferenceMonth);
        }
        else
        {
            if (!MonthProjectionService.TryNormalizeReferenceMonth(desiredReferenceMonth, out nextReferenceMonth))
                throw new DomainValidationException("referenceMonth must be in format YYYY-MM.");
        }

        var exists = await dbContext.Months.AnyAsync(x => x.ReferenceMonth == nextReferenceMonth, cancellationToken);
        if (exists)
            throw new ConflictException($"Month {nextReferenceMonth} already exists.");

        openMonth.Status = MonthStatus.Closed;
        openMonth.ClosedAt = DateTimeOffset.UtcNow;

        var newMonthId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var newMonth = new Month
        {
            Id = newMonthId,
            ReferenceMonth = nextReferenceMonth,
            Salary = openMonth.Salary,
            Currency = openMonth.Currency,
            Status = MonthStatus.Open,
            CreatedAt = now,
            ClonedFromMonthId = openMonth.Id
        };

        var newCategories = openMonth.CategoryBudgets
            .OrderBy(x => x.DisplayOrder)
            .Select(category => new CategoryBudget
            {
                Id = Guid.NewGuid(),
                MonthId = newMonthId,
                Name = CategoryNames.Normalize(category.Name),
                GroupName = GroupNames.Normalize(category.GroupName),
                PlannedAmount = category.PlannedAmount,
                DisplayOrder = category.DisplayOrder
            })
            .ToList();

        var newTargets = openMonth.GroupTargets
            .GroupBy(target => GroupNames.Normalize(target.GroupName), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var source = group.First();
                return new GroupTarget
                {
                    Id = Guid.NewGuid(),
                    MonthId = newMonthId,
                    GroupName = GroupNames.Normalize(group.Key),
                    TargetPercent = source.TargetPercent
                };
            })
            .ToList();

        foreach (var groupName in GroupNames.All)
        {
            if (newTargets.Any(target => string.Equals(target.GroupName, groupName, StringComparison.OrdinalIgnoreCase)))
                continue;

            newTargets.Add(new GroupTarget
            {
                Id = Guid.NewGuid(),
                MonthId = newMonthId,
                GroupName = groupName,
                TargetPercent = 0m
            });
        }

        await dbContext.Months.AddAsync(newMonth, cancellationToken);
        await dbContext.CategoryBudgets.AddRangeAsync(newCategories, cancellationToken);
        await dbContext.GroupTargets.AddRangeAsync(newTargets, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var monthWithDetails = await dbContext.Months
            .WithDetails()
            .FirstAsync(x => x.Id == newMonthId, cancellationToken);

        return projectionService.ToMonthSummary(monthWithDetails);
    }

    public async Task<MonthSummaryDto> UpdateSalaryAsync(Guid monthId, decimal salary, CancellationToken cancellationToken)
    {
        if (salary < 0)
            throw new DomainValidationException("Salary must be greater than or equal to zero.");

        var month = await dbContext.Months
            .WithDetails()
            .FirstOrDefaultAsync(x => x.Id == monthId, cancellationToken);

        if (month is null)
            throw new NotFoundException();

        if (!MonthProjectionService.IsOpen(month))
            throw new ConflictException("Closed months cannot be edited.");

        month.Salary = decimal.Round(salary, 2);
        await dbContext.SaveChangesAsync(cancellationToken);

        return projectionService.ToMonthSummary(month);
    }
}
