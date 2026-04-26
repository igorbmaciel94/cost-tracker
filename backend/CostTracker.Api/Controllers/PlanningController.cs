using CostTracker.Api.Contracts;
using CostTracker.Domain.Entities;
using CostTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CostTracker.Api.Controllers;

[ApiController]
[Route("api/planning/goals")]
public class PlanningController(CostTrackerDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PlanningGoalDto>>> GetGoals(CancellationToken cancellationToken)
    {
        var goals = await dbContext.PlanningGoals
            .OrderBy(x => x.CreatedAt)
            .Select(x => new PlanningGoalDto(x.Id, x.Name, x.TotalAmount, x.SavedAmount, x.Months))
            .ToListAsync(cancellationToken);

        return Ok(goals);
    }

    [HttpPost]
    public async Task<ActionResult<IReadOnlyList<PlanningGoalDto>>> CreateGoal(
        [FromBody] CreatePlanningGoalRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("name is required.");

        if (request.TotalAmount < 0)
            return BadRequest("totalAmount must be greater than or equal to zero.");

        if (request.SavedAmount < 0)
            return BadRequest("savedAmount must be greater than or equal to zero.");

        if (request.SavedAmount > request.TotalAmount)
            return BadRequest("savedAmount cannot exceed totalAmount.");

        if (request.Months < 1)
            return BadRequest("months must be at least 1.");

        var goal = new PlanningGoal
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            TotalAmount = decimal.Round(request.TotalAmount, 2),
            SavedAmount = decimal.Round(request.SavedAmount, 2),
            Months = request.Months,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await dbContext.PlanningGoals.AddAsync(goal, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetGoals(cancellationToken);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<IReadOnlyList<PlanningGoalDto>>> UpdateGoal(
        Guid id,
        [FromBody] UpdatePlanningGoalRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("name is required.");

        if (request.TotalAmount < 0)
            return BadRequest("totalAmount must be greater than or equal to zero.");

        if (request.SavedAmount < 0)
            return BadRequest("savedAmount must be greater than or equal to zero.");

        if (request.SavedAmount > request.TotalAmount)
            return BadRequest("savedAmount cannot exceed totalAmount.");

        if (request.Months < 1)
            return BadRequest("months must be at least 1.");

        var goal = await dbContext.PlanningGoals.FindAsync([id], cancellationToken);
        if (goal is null)
            return NotFound();

        goal.Name = request.Name.Trim();
        goal.TotalAmount = decimal.Round(request.TotalAmount, 2);
        goal.SavedAmount = decimal.Round(request.SavedAmount, 2);
        goal.Months = request.Months;

        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetGoals(cancellationToken);
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<IReadOnlyList<PlanningGoalDto>>> DeleteGoal(
        Guid id,
        CancellationToken cancellationToken)
    {
        var goal = await dbContext.PlanningGoals.FindAsync([id], cancellationToken);
        if (goal is null)
            return NotFound();

        dbContext.PlanningGoals.Remove(goal);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetGoals(cancellationToken);
    }
}
