using CostTracker.Api.Contracts;
using CostTracker.Api.Services;
using CostTracker.Domain.Constants;
using CostTracker.Domain.Entities;
using CostTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CostTracker.Api.Controllers;

[ApiController]
[Route("api/months/{monthId:guid}/budget")]
public class BudgetController(
    CostTrackerDbContext dbContext,
    MonthProjectionService projectionService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<BudgetResponseDto>> GetBudget(Guid monthId, CancellationToken cancellationToken)
    {
        var month = await dbContext.Months
            .WithDetails()
            .FirstOrDefaultAsync(x => x.Id == monthId, cancellationToken);

        if (month is null)
        {
            return NotFound();
        }

        return Ok(projectionService.ToBudgetResponse(month));
    }

    [HttpPost("categories")]
    public async Task<ActionResult<BudgetResponseDto>> CreateCategory(
        Guid monthId,
        [FromBody] CreateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.GroupName))
        {
            return BadRequest("name and groupName are required.");
        }

        if (request.PlannedAmount < 0)
        {
            return BadRequest("plannedAmount must be greater than or equal to zero.");
        }

        var normalizedName = CategoryNames.Normalize(request.Name);
        var normalizedGroupName = GroupNames.Normalize(request.GroupName);

        var month = await dbContext.Months
            .WithDetails()
            .FirstOrDefaultAsync(x => x.Id == monthId, cancellationToken);

        if (month is null)
        {
            return NotFound();
        }

        if (!MonthProjectionService.IsOpen(month))
        {
            return Conflict("Closed months cannot be edited.");
        }

        var alreadyExists = month.CategoryBudgets
            .Any(x => string.Equals(x.Name, normalizedName, StringComparison.OrdinalIgnoreCase));

        if (alreadyExists)
        {
            return Conflict("Category name already exists for this month.");
        }

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

        return Ok(projectionService.ToBudgetResponse(updatedMonth));
    }

    [HttpPut("categories/{categoryId:guid}")]
    public async Task<ActionResult<BudgetResponseDto>> UpdateCategory(
        Guid monthId,
        Guid categoryId,
        [FromBody] UpdateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.GroupName))
        {
            return BadRequest("name and groupName are required.");
        }

        if (request.PlannedAmount < 0)
        {
            return BadRequest("plannedAmount must be greater than or equal to zero.");
        }

        var normalizedName = CategoryNames.Normalize(request.Name);
        var normalizedGroupName = GroupNames.Normalize(request.GroupName);

        var month = await dbContext.Months
            .WithDetails()
            .FirstOrDefaultAsync(x => x.Id == monthId, cancellationToken);

        if (month is null)
        {
            return NotFound();
        }

        if (!MonthProjectionService.IsOpen(month))
        {
            return Conflict("Closed months cannot be edited.");
        }

        var category = month.CategoryBudgets.FirstOrDefault(x => x.Id == categoryId);
        if (category is null)
        {
            return NotFound();
        }

        var alreadyExists = month.CategoryBudgets
            .Any(x => x.Id != categoryId && string.Equals(x.Name, normalizedName, StringComparison.OrdinalIgnoreCase));

        if (alreadyExists)
        {
            return Conflict("Category name already exists for this month.");
        }

        category.Name = normalizedName;
        category.GroupName = normalizedGroupName;
        category.PlannedAmount = decimal.Round(request.PlannedAmount, 2);

        if (request.DisplayOrder is not null)
        {
            category.DisplayOrder = request.DisplayOrder.Value;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var updatedMonth = await dbContext.Months
            .WithDetails()
            .FirstAsync(x => x.Id == monthId, cancellationToken);

        return Ok(projectionService.ToBudgetResponse(updatedMonth));
    }

    [HttpDelete("categories/{categoryId:guid}")]
    public async Task<ActionResult<BudgetResponseDto>> DeleteCategory(
        Guid monthId,
        Guid categoryId,
        CancellationToken cancellationToken)
    {
        var month = await dbContext.Months
            .WithDetails()
            .FirstOrDefaultAsync(x => x.Id == monthId, cancellationToken);

        if (month is null)
        {
            return NotFound();
        }

        if (!MonthProjectionService.IsOpen(month))
        {
            return Conflict("Closed months cannot be edited.");
        }

        var category = month.CategoryBudgets.FirstOrDefault(x => x.Id == categoryId);
        if (category is null)
        {
            return NotFound();
        }

        var hasEntries = await dbContext.Entries.AnyAsync(x => x.CategoryBudgetId == categoryId, cancellationToken);
        if (hasEntries)
        {
            return Conflict("Cannot delete category with entries.");
        }

        dbContext.CategoryBudgets.Remove(category);
        await dbContext.SaveChangesAsync(cancellationToken);

        var updatedMonth = await dbContext.Months
            .WithDetails()
            .FirstAsync(x => x.Id == monthId, cancellationToken);

        return Ok(projectionService.ToBudgetResponse(updatedMonth));
    }
}
