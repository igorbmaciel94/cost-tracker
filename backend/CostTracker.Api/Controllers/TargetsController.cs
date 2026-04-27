using CostTracker.Application.Contracts;
using CostTracker.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace CostTracker.Api.Controllers;

[ApiController]
[Route("api/months/{monthId:guid}/targets")]
public class TargetsController(TargetsService targetsService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<TargetsResponseDto>> GetTargets(Guid monthId, CancellationToken ct)
        => Ok(await targetsService.GetTargetsAsync(monthId, ct));

    [HttpPut]
    public async Task<ActionResult<TargetsResponseDto>> UpdateTargets(
        Guid monthId,
        [FromBody] UpdateTargetsRequest request,
        CancellationToken ct)
        => Ok(await targetsService.UpdateTargetsAsync(monthId, request, ct));
}
