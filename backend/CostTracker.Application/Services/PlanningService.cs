using CostTracker.Application.Contracts;
using CostTracker.Application.Exceptions;
using CostTracker.Application.Interfaces;
using CostTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CostTracker.Application.Services;

public class PlanningService(ICostTrackerDbContext dbContext)
{
    public async Task<IReadOnlyList<PlanningGoalDto>> GetGoalsAsync(CancellationToken cancellationToken)
    {
        return await dbContext.PlanningGoals
            .OrderBy(x => x.CreatedAt)
            .Select(x => new PlanningGoalDto(x.Id, x.Name, x.TotalAmount, x.SavedAmount, x.Months))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PlanningGoalDto>> CreateGoalAsync(CreatePlanningGoalRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new DomainValidationException("name is required.");

        if (request.TotalAmount < 0)
            throw new DomainValidationException("totalAmount must be greater than or equal to zero.");

        if (request.SavedAmount < 0)
            throw new DomainValidationException("savedAmount must be greater than or equal to zero.");

        if (request.SavedAmount > request.TotalAmount)
            throw new DomainValidationException("savedAmount cannot exceed totalAmount.");

        if (request.Months < 1)
            throw new DomainValidationException("months must be at least 1.");

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

        return await GetGoalsAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PlanningGoalDto>> UpdateGoalAsync(Guid id, UpdatePlanningGoalRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new DomainValidationException("name is required.");

        if (request.TotalAmount < 0)
            throw new DomainValidationException("totalAmount must be greater than or equal to zero.");

        if (request.SavedAmount < 0)
            throw new DomainValidationException("savedAmount must be greater than or equal to zero.");

        if (request.SavedAmount > request.TotalAmount)
            throw new DomainValidationException("savedAmount cannot exceed totalAmount.");

        if (request.Months < 1)
            throw new DomainValidationException("months must be at least 1.");

        var goal = await dbContext.PlanningGoals.FindAsync([id], cancellationToken);
        if (goal is null)
            throw new NotFoundException();

        goal.Name = request.Name.Trim();
        goal.TotalAmount = decimal.Round(request.TotalAmount, 2);
        goal.SavedAmount = decimal.Round(request.SavedAmount, 2);
        goal.Months = request.Months;

        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetGoalsAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PlanningGoalDto>> DeleteGoalAsync(Guid id, CancellationToken cancellationToken)
    {
        var goal = await dbContext.PlanningGoals.FindAsync([id], cancellationToken);
        if (goal is null)
            throw new NotFoundException();

        dbContext.PlanningGoals.Remove(goal);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetGoalsAsync(cancellationToken);
    }
}
