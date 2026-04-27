using CostTracker.Application.Contracts;
using CostTracker.Application.Exceptions;
using CostTracker.Application.Interfaces;
using CostTracker.Application.Projections;
using Microsoft.EntityFrameworkCore;

namespace CostTracker.Application.Services;

public class DashboardService(ICostTrackerDbContext dbContext, MonthProjectionService projectionService)
{
    public async Task<DashboardDto> GetDashboardAsync(Guid monthId, CancellationToken cancellationToken)
    {
        var month = await dbContext.Months
            .WithDetails()
            .FirstOrDefaultAsync(x => x.Id == monthId, cancellationToken);

        if (month is null)
            throw new NotFoundException();

        return projectionService.ToDashboard(month);
    }
}
