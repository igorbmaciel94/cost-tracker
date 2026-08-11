using CostTracker.Application.Contracts;
using CostTracker.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace CostTracker.Api.Controllers;

[ApiController]
[Route("api/investments/contribution-plans")]
public sealed class InvestmentContributionPlansController(
    ContributionPlanningService contributionPlanningService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ContributionPlanDto>>> GetPlans(
        CancellationToken cancellationToken)
        => Ok(await contributionPlanningService.GetPlansAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ContributionPlanDto>> GetPlan(
        Guid id,
        CancellationToken cancellationToken)
        => Ok(await contributionPlanningService.GetPlanAsync(id, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<ContributionPlanDto>> CreatePlan(
        [FromBody] CreateContributionPlanRequest request,
        CancellationToken cancellationToken)
    {
        var result = await contributionPlanningService.CreatePlanAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetPlan), new { id = result.Id }, result);
    }

    [HttpPost("{id:guid}/confirm")]
    public async Task<ActionResult<ContributionPlanDto>> ConfirmPlan(
        Guid id,
        [FromBody] ConfirmContributionPlanRequest request,
        CancellationToken cancellationToken)
        => Ok(await contributionPlanningService.ConfirmPlanAsync(id, request, cancellationToken));
}
