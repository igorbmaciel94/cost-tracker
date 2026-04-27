using CostTracker.Application.Contracts;
using CostTracker.Application.Exceptions;
using CostTracker.Application.Interfaces;
using CostTracker.Application.Projections;
using CostTracker.Domain.Constants;
using CostTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CostTracker.Application.Services;

public class TargetsService(ICostTrackerDbContext dbContext, MonthProjectionService projectionService)
{
    public async Task<TargetsResponseDto> GetTargetsAsync(Guid monthId, CancellationToken cancellationToken)
    {
        var month = await dbContext.Months
            .WithDetails()
            .FirstOrDefaultAsync(x => x.Id == monthId, cancellationToken);

        if (month is null)
            throw new NotFoundException();

        return projectionService.ToTargetsResponse(month);
    }

    public async Task<TargetsResponseDto> UpdateTargetsAsync(Guid monthId, UpdateTargetsRequest request, CancellationToken cancellationToken)
    {
        if (request.Items.Count == 0)
            throw new DomainValidationException("At least one target item is required.");

        var month = await dbContext.Months
            .WithDetails()
            .FirstOrDefaultAsync(x => x.Id == monthId, cancellationToken);

        if (month is null)
            throw new NotFoundException();

        if (!MonthProjectionService.IsOpen(month))
            throw new ConflictException("Closed months cannot be edited.");

        foreach (var item in request.Items)
        {
            if (string.IsNullOrWhiteSpace(item.GroupName))
                throw new DomainValidationException("groupName is required.");

            if (item.TargetPercent < 0 || item.TargetPercent > 1)
                throw new DomainValidationException("targetPercent must be between 0 and 1.");

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

        return projectionService.ToTargetsResponse(updatedMonth);
    }
}
