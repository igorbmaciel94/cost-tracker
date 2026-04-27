using CostTracker.Application.Contracts;
using CostTracker.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace CostTracker.Api.Controllers;

[ApiController]
[Route("api/planning/goals")]
public class PlanningController(PlanningService planningService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PlanningGoalDto>>> GetGoals(CancellationToken ct)
        => Ok(await planningService.GetGoalsAsync(ct));

    [HttpPost]
    public async Task<ActionResult<IReadOnlyList<PlanningGoalDto>>> CreateGoal(
        [FromBody] CreatePlanningGoalRequest request,
        CancellationToken ct)
        => Ok(await planningService.CreateGoalAsync(request, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<IReadOnlyList<PlanningGoalDto>>> UpdateGoal(
        Guid id,
        [FromBody] UpdatePlanningGoalRequest request,
        CancellationToken ct)
        => Ok(await planningService.UpdateGoalAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<IReadOnlyList<PlanningGoalDto>>> DeleteGoal(Guid id, CancellationToken ct)
        => Ok(await planningService.DeleteGoalAsync(id, ct));
}
