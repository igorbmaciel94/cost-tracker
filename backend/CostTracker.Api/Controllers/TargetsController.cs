using CostTracker.Api.Contracts;
using CostTracker.Api.Services;
using CostTracker.Domain.Constants;
using CostTracker.Domain.Entities;
using CostTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CostTracker.Api.Controllers;

[ApiController]
[Route("api/months/{monthId:guid}/targets")]
public class TargetsController(
    CostTrackerDbContext dbContext,
    MonthProjectionService projectionService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<TargetsResponseDto>> GetTargets(Guid monthId, CancellationToken cancellationToken)
    {
        var month = await dbContext.Months
            .WithDetails()
            .FirstOrDefaultAsync(x => x.Id == monthId, cancellationToken);

        if (month is null)
        {
            return NotFound();
        }

        return Ok(projectionService.ToTargetsResponse(month));
    }

    [HttpPut]
    public async Task<ActionResult<TargetsResponseDto>> UpdateTargets(
        Guid monthId,
        [FromBody] UpdateTargetsRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Items.Count == 0)
        {
            return BadRequest("At least one target item is required.");
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

        foreach (var item in request.Items)
        {
            if (string.IsNullOrWhiteSpace(item.GroupName))
            {
                return BadRequest("groupName is required.");
            }

            if (item.TargetPercent < 0 || item.TargetPercent > 1)
            {
                return BadRequest("targetPercent must be between 0 and 1.");
            }

            var normalizedGroupName = GroupNames.Normalize(item.GroupName);
            var existing = month.GroupTargets
                .FirstOrDefault(x => string.Equals(
                    GroupNames.Normalize(x.GroupName),
                    normalizedGroupName,
                    StringComparison.OrdinalIgnoreCase));

            if (existing is null)
            {
                month.GroupTargets.Add(new GroupTarget
                {
                    Id = Guid.NewGuid(),
                    MonthId = month.Id,
                    GroupName = normalizedGroupName,
                    TargetPercent = decimal.Round(item.TargetPercent, 4)
                });
            }
            else
            {
                existing.TargetPercent = decimal.Round(item.TargetPercent, 4);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var updatedMonth = await dbContext.Months
            .WithDetails()
            .FirstAsync(x => x.Id == monthId, cancellationToken);

        return Ok(projectionService.ToTargetsResponse(updatedMonth));
    }
}
