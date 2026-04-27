using CostTracker.Application.Contracts;
using CostTracker.Application.Exceptions;
using CostTracker.Application.Interfaces;
using CostTracker.Application.Projections;
using CostTracker.Domain.Constants;
using CostTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CostTracker.Application.Services;

public class BudgetService(ICostTrackerDbContext dbContext, MonthProjectionService projectionService)
{
    public async Task<BudgetResponseDto> GetBudgetAsync(Guid monthId, CancellationToken cancellationToken)
    {
        var month = await dbContext.Months
            .WithDetails()
            .FirstOrDefaultAsync(x => x.Id == monthId, cancellationToken);

        if (month is null)
            throw new NotFoundException();

        return projectionService.ToBudgetResponse(month);
    }

    public async Task<BudgetResponseDto> CreateCategoryAsync(
        Guid monthId,
        CreateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.GroupName))
            throw new DomainValidationException("name and groupName are required.");

        if (request.PlannedAmount < 0)
            throw new DomainValidationException("plannedAmount must be greater than or equal to zero.");

        var normalizedName = CategoryNames.Normalize(request.Name);
        var normalizedGroupName = GroupNames.Normalize(request.GroupName);

        var month = await dbContext.Months
            .WithDetails()
            .FirstOrDefaultAsync(x => x.Id == monthId, cancellationToken);

        if (month is null)
            throw new NotFoundException();

        if (!MonthProjectionService.IsOpen(month))
            throw new ConflictException("Closed months cannot be edited.");

        if (month.CategoryBudgets.Any(x => string.Equals(x.Name, normalizedName, StringComparison.OrdinalIgnoreCase)))
            throw new ConflictException("Category name already exists for this month.");

        var displayOrder = request.DisplayOrder ?? month.CategoryBudgets.Select(x => x.DisplayOrder).DefaultIfEmpty(0).Max() + 1;

        var category = new CategoryBudget
        {
            Id = Guid.NewGuid(),
            MonthId = monthId,
            Name = normalizedName,
            GroupName = normalizedGroupName,
            PlannedAmount = decimal.Round(request.PlannedAmount, 2),
            DisplayOrder = displayOrder
        };

        await dbContext.CategoryBudgets.AddAsync(category, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var updatedMonth = await dbContext.Months
            .WithDetails()
            .FirstAsync(x => x.Id == monthId, cancellationToken);

        return projectionService.ToBudgetResponse(updatedMonth);
    }

    public async Task<BudgetResponseDto> UpdateCategoryAsync(
        Guid monthId,
        Guid categoryId,
        UpdateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.GroupName))
            throw new DomainValidationException("name and groupName are required.");

        if (request.PlannedAmount < 0)
            throw new DomainValidationException("plannedAmount must be greater than or equal to zero.");

        var normalizedName = CategoryNames.Normalize(request.Name);
        var normalizedGroupName = GroupNames.Normalize(request.GroupName);

        var month = await dbContext.Months
            .WithDetails()
            .FirstOrDefaultAsync(x => x.Id == monthId, cancellationToken);

        if (month is null)
            throw new NotFoundException();

        if (!MonthProjectionService.IsOpen(month))
            throw new ConflictException("Closed months cannot be edited.");

        var category = month.CategoryBudgets.FirstOrDefault(x => x.Id == categoryId);
        if (category is null)
            throw new NotFoundException();

        if (month.CategoryBudgets.Any(x => x.Id != categoryId && string.Equals(x.Name, normalizedName, StringComparison.OrdinalIgnoreCase)))
            throw new ConflictException("Category name already exists for this month.");

        category.Name = normalizedName;
        category.GroupName = normalizedGroupName;
        category.PlannedAmount = decimal.Round(request.PlannedAmount, 2);

        if (request.DisplayOrder is not null)
            category.DisplayOrder = request.DisplayOrder.Value;

        await dbContext.SaveChangesAsync(cancellationToken);

        var updatedMonth = await dbContext.Months
            .WithDetails()
            .FirstAsync(x => x.Id == monthId, cancellationToken);

        return projectionService.ToBudgetResponse(updatedMonth);
    }

    public async Task<BudgetResponseDto> DeleteCategoryAsync(Guid monthId, Guid categoryId, CancellationToken cancellationToken)
    {
        var month = await dbContext.Months
            .WithDetails()
            .FirstOrDefaultAsync(x => x.Id == monthId, cancellationToken);

        if (month is null)
            throw new NotFoundException();

        if (!MonthProjectionService.IsOpen(month))
            throw new ConflictException("Closed months cannot be edited.");

        var category = month.CategoryBudgets.FirstOrDefault(x => x.Id == categoryId);
        if (category is null)
            throw new NotFoundException();

        var hasEntries = await dbContext.Entries.AnyAsync(x => x.CategoryBudgetId == categoryId, cancellationToken);
        if (hasEntries)
            throw new ConflictException("Cannot delete category with entries.");

        dbContext.CategoryBudgets.Remove(category);
        await dbContext.SaveChangesAsync(cancellationToken);

        var updatedMonth = await dbContext.Months
            .WithDetails()
            .FirstAsync(x => x.Id == monthId, cancellationToken);

        return projectionService.ToBudgetResponse(updatedMonth);
    }
}
