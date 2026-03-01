using CostTracker.Api.Contracts;
using CostTracker.Api.Services;
using CostTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CostTracker.Api.Controllers;

[ApiController]
[Route("api/months/{monthId:guid}/dashboard")]
public class DashboardController(
    CostTrackerDbContext dbContext,
    MonthProjectionService projectionService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DashboardDto>> GetDashboard(Guid monthId, CancellationToken cancellationToken)
    {
        var month = await dbContext.Months
            .WithDetails()
            .FirstOrDefaultAsync(x => x.Id == monthId, cancellationToken);

        if (month is null)
        {
            return NotFound();
        }

        return Ok(projectionService.ToDashboard(month));
    }
}
