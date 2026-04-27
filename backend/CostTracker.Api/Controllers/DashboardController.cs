using CostTracker.Application.Contracts;
using CostTracker.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace CostTracker.Api.Controllers;

[ApiController]
[Route("api/months/{monthId:guid}/dashboard")]
public class DashboardController(DashboardService dashboardService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DashboardDto>> GetDashboard(Guid monthId, CancellationToken ct)
        => Ok(await dashboardService.GetDashboardAsync(monthId, ct));
}
