using CostTracker.Api.Contracts;
using CostTracker.Api.Services;
using CostTracker.Domain.Entities;
using CostTracker.Domain.Enums;
using CostTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CostTracker.Api.Controllers;

[ApiController]
[Route("api/months")]
public class MonthsController(
    CostTrackerDbContext dbContext,
    MonthProjectionService projectionService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MonthSummaryDto>>> GetMonths(CancellationToken cancellationToken)
    {
        var months = await dbContext.Months
            .WithDetails()
            .OrderByDescending(x => x.ReferenceMonth)
            .ToListAsync(cancellationToken);

        return Ok(months.Select(projectionService.ToMonthSummary).ToList());
    }

    [HttpPost("new")]
    public async Task<ActionResult<MonthSummaryDto>> CreateNewMonth(
        [FromBody] CreateMonthRequest? request,
        CancellationToken cancellationToken)
    {
        var openMonth = await dbContext.Months
            .WithDetails()
            .FirstOrDefaultAsync(x => x.Status == MonthStatus.Open, cancellationToken);

        if (openMonth is null)
        {
            return NotFound("No OPEN month found to clone.");
        }

        var desiredReferenceMonth = request?.ReferenceMonth;
        string nextReferenceMonth;

        if (string.IsNullOrWhiteSpace(desiredReferenceMonth))
        {
            nextReferenceMonth = MonthProjectionService.GetNextReferenceMonth(openMonth.ReferenceMonth);
        }
        else
        {
            if (!MonthProjectionService.TryNormalizeReferenceMonth(desiredReferenceMonth, out nextReferenceMonth))
            {
                return BadRequest("referenceMonth must be in format YYYY-MM.");
            }
        }

        var exists = await dbContext.Months.AnyAsync(x => x.ReferenceMonth == nextReferenceMonth, cancellationToken);
        if (exists)
        {
            return Conflict($"Month {nextReferenceMonth} already exists.");
        }

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
                Name = category.Name,
                GroupName = category.GroupName,
                PlannedAmount = category.PlannedAmount,
                DisplayOrder = category.DisplayOrder
            })
            .ToList();

        var newTargets = openMonth.GroupTargets
            .Select(target => new GroupTarget
            {
                Id = Guid.NewGuid(),
                MonthId = newMonthId,
                GroupName = target.GroupName,
                TargetPercent = target.TargetPercent
            })
            .ToList();

        await dbContext.Months.AddAsync(newMonth, cancellationToken);
        await dbContext.CategoryBudgets.AddRangeAsync(newCategories, cancellationToken);
        await dbContext.GroupTargets.AddRangeAsync(newTargets, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var monthWithDetails = await dbContext.Months
            .WithDetails()
            .FirstAsync(x => x.Id == newMonthId, cancellationToken);

        return Created($"/api/months/{monthWithDetails.Id}", projectionService.ToMonthSummary(monthWithDetails));
    }

    [HttpPut("{monthId:guid}/salary")]
    public async Task<ActionResult<MonthSummaryDto>> UpdateSalary(
        Guid monthId,
        [FromBody] UpdateSalaryRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Salary < 0)
        {
            return BadRequest("Salary must be greater than or equal to zero.");
        }

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

        month.Salary = decimal.Round(request.Salary, 2);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(projectionService.ToMonthSummary(month));
    }
}
