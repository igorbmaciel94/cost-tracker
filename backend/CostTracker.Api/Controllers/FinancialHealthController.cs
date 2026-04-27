using CostTracker.Application.Contracts;
using CostTracker.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace CostTracker.Api.Controllers;

[ApiController]
[Route("api/financial-health/profile")]
public class FinancialHealthController(FinancialHealthService financialHealthService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<HealthProfileDto>> GetProfile(CancellationToken ct)
        => Ok(await financialHealthService.GetProfileAsync(ct));

    [HttpPut]
    public async Task<ActionResult<HealthProfileDto>> UpsertProfile(
        [FromBody] UpdateHealthProfileRequest request,
        CancellationToken ct)
        => Ok(await financialHealthService.UpsertProfileAsync(request, ct));
}
