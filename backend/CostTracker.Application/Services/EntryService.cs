using CostTracker.Application.Contracts;
using CostTracker.Application.Exceptions;
using CostTracker.Application.Interfaces;
using CostTracker.Application.Projections;
using CostTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CostTracker.Application.Services;

public class EntryService(ICostTrackerDbContext dbContext, MonthProjectionService projectionService)
{
    public async Task<EntriesResponseDto> GetEntriesAsync(Guid monthId, CancellationToken cancellationToken)
    {
        var month = await dbContext.Months
            .WithDetails()
            .FirstOrDefaultAsync(x => x.Id == monthId, cancellationToken);

        if (month is null)
            throw new NotFoundException();

        return projectionService.ToEntriesResponse(month);
    }

    public async Task<EntriesResponseDto> CreateEntryAsync(Guid monthId, CreateEntryRequest request, CancellationToken cancellationToken)
    {
        if (request.Amount < 0)
            throw new DomainValidationException("amount must be greater than or equal to zero.");

        if (string.IsNullOrWhiteSpace(request.Description))
            throw new DomainValidationException("description is required.");

        var month = await dbContext.Months
            .WithDetails()
            .FirstOrDefaultAsync(x => x.Id == monthId, cancellationToken);

        if (month is null)
            throw new NotFoundException();

        if (!MonthProjectionService.IsOpen(month))
            throw new ConflictException("Closed months cannot be edited.");

        var category = month.CategoryBudgets.FirstOrDefault(x => x.Id == request.CategoryBudgetId);
        if (category is null)
            throw new DomainValidationException("categoryBudgetId does not belong to this month.");

        var entry = new Entry
        {
            Id = Guid.NewGuid(),
            MonthId = monthId,
            CategoryBudgetId = request.CategoryBudgetId,
            EntryDate = request.EntryDate,
            Description = request.Description.Trim(),
            Amount = decimal.Round(request.Amount, 2),
            CreatedAt = DateTimeOffset.UtcNow
        };

        await dbContext.Entries.AddAsync(entry, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var updatedMonth = await dbContext.Months
            .WithDetails()
            .FirstAsync(x => x.Id == monthId, cancellationToken);

        return projectionService.ToEntriesResponse(updatedMonth);
    }

    public async Task<EntriesResponseDto> UpdateEntryAsync(Guid monthId, Guid entryId, UpdateEntryRequest request, CancellationToken cancellationToken)
    {
        if (request.Amount < 0)
            throw new DomainValidationException("amount must be greater than or equal to zero.");

        if (string.IsNullOrWhiteSpace(request.Description))
            throw new DomainValidationException("description is required.");

        var month = await dbContext.Months
            .WithDetails()
            .FirstOrDefaultAsync(x => x.Id == monthId, cancellationToken);

        if (month is null)
            throw new NotFoundException();

        if (!MonthProjectionService.IsOpen(month))
            throw new ConflictException("Closed months cannot be edited.");

        var entry = month.Entries.FirstOrDefault(x => x.Id == entryId);
        if (entry is null)
            throw new NotFoundException();

        var category = month.CategoryBudgets.FirstOrDefault(x => x.Id == request.CategoryBudgetId);
        if (category is null)
            throw new DomainValidationException("categoryBudgetId does not belong to this month.");

        entry.CategoryBudgetId = request.CategoryBudgetId;
        entry.EntryDate = request.EntryDate;
        entry.Description = request.Description.Trim();
        entry.Amount = decimal.Round(request.Amount, 2);

        await dbContext.SaveChangesAsync(cancellationToken);

        var updatedMonth = await dbContext.Months
            .WithDetails()
            .FirstAsync(x => x.Id == monthId, cancellationToken);

        return projectionService.ToEntriesResponse(updatedMonth);
    }

    public async Task<EntriesResponseDto> DeleteEntryAsync(Guid monthId, Guid entryId, CancellationToken cancellationToken)
    {
        var month = await dbContext.Months
            .WithDetails()
            .FirstOrDefaultAsync(x => x.Id == monthId, cancellationToken);

        if (month is null)
            throw new NotFoundException();

        if (!MonthProjectionService.IsOpen(month))
            throw new ConflictException("Closed months cannot be edited.");

        var entry = month.Entries.FirstOrDefault(x => x.Id == entryId);
        if (entry is null)
            throw new NotFoundException();

        dbContext.Entries.Remove(entry);
        await dbContext.SaveChangesAsync(cancellationToken);

        var updatedMonth = await dbContext.Months
            .WithDetails()
            .FirstAsync(x => x.Id == monthId, cancellationToken);

        return projectionService.ToEntriesResponse(updatedMonth);
    }
}
