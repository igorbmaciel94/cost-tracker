using CostTracker.Api.Contracts;
using CostTracker.Api.Services;
using CostTracker.Domain.Entities;
using CostTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CostTracker.Api.Controllers;

[ApiController]
[Route("api/months/{monthId:guid}/entries")]
public class EntriesController(
    CostTrackerDbContext dbContext,
    MonthProjectionService projectionService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<EntriesResponseDto>> GetEntries(Guid monthId, CancellationToken cancellationToken)
    {
        var month = await dbContext.Months
            .WithDetails()
            .FirstOrDefaultAsync(x => x.Id == monthId, cancellationToken);

        if (month is null)
        {
            return NotFound();
        }

        return Ok(projectionService.ToEntriesResponse(month));
    }

    [HttpPost]
    public async Task<ActionResult<EntriesResponseDto>> CreateEntry(
        Guid monthId,
        [FromBody] CreateEntryRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Amount < 0)
        {
            return BadRequest("amount must be greater than or equal to zero.");
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            return BadRequest("description is required.");
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

        var category = month.CategoryBudgets.FirstOrDefault(x => x.Id == request.CategoryBudgetId);
        if (category is null)
        {
            return BadRequest("categoryBudgetId does not belong to this month.");
        }

        var entry = new Entry
        {
            Id = Guid.NewGuid(),
            MonthId = monthId,
            CategoryBudgetId = request.CategoryBudgetId,
            EntryDate = request.EntryDate,
            Description = request.Description.Trim(),
            Amount = decimal.Round(request.Amount, 2),
            CreatedAt = DateTimeOffset.UtcNow
        };

        await dbContext.Entries.AddAsync(entry, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var updatedMonth = await dbContext.Months
            .WithDetails()
            .FirstAsync(x => x.Id == monthId, cancellationToken);

        return Ok(projectionService.ToEntriesResponse(updatedMonth));
    }

    [HttpPut("{entryId:guid}")]
    public async Task<ActionResult<EntriesResponseDto>> UpdateEntry(
        Guid monthId,
        Guid entryId,
        [FromBody] UpdateEntryRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Amount < 0)
        {
            return BadRequest("amount must be greater than or equal to zero.");
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            return BadRequest("description is required.");
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

        var entry = month.Entries.FirstOrDefault(x => x.Id == entryId);
        if (entry is null)
        {
            return NotFound();
        }

        var category = month.CategoryBudgets.FirstOrDefault(x => x.Id == request.CategoryBudgetId);
        if (category is null)
        {
            return BadRequest("categoryBudgetId does not belong to this month.");
        }

        entry.CategoryBudgetId = request.CategoryBudgetId;
        entry.EntryDate = request.EntryDate;
        entry.Description = request.Description.Trim();
        entry.Amount = decimal.Round(request.Amount, 2);

        await dbContext.SaveChangesAsync(cancellationToken);

        var updatedMonth = await dbContext.Months
            .WithDetails()
            .FirstAsync(x => x.Id == monthId, cancellationToken);

        return Ok(projectionService.ToEntriesResponse(updatedMonth));
    }

    [HttpDelete("{entryId:guid}")]
    public async Task<ActionResult<EntriesResponseDto>> DeleteEntry(
        Guid monthId,
        Guid entryId,
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

        var entry = month.Entries.FirstOrDefault(x => x.Id == entryId);
        if (entry is null)
        {
            return NotFound();
        }

        dbContext.Entries.Remove(entry);
        await dbContext.SaveChangesAsync(cancellationToken);

        var updatedMonth = await dbContext.Months
            .WithDetails()
            .FirstAsync(x => x.Id == monthId, cancellationToken);

        return Ok(projectionService.ToEntriesResponse(updatedMonth));
    }
}
