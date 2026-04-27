using CostTracker.Application.Contracts;
using CostTracker.Application.Exceptions;
using CostTracker.Application.Interfaces;
using CostTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CostTracker.Application.Services;

public class FinancialHealthService(ICostTrackerDbContext dbContext)
{
    public async Task<HealthProfileDto> GetProfileAsync(CancellationToken cancellationToken)
    {
        var profile = await dbContext.HealthProfiles.FirstOrDefaultAsync(cancellationToken);
        if (profile is null)
            return new HealthProfileDto(Guid.Empty, 0, 0);

        return new HealthProfileDto(profile.Id, profile.EssentialExpenses, profile.SavedEmergencyFund);
    }

    public async Task<HealthProfileDto> UpsertProfileAsync(UpdateHealthProfileRequest request, CancellationToken cancellationToken)
    {
        if (request.EssentialExpenses < 0)
            throw new DomainValidationException("essentialExpenses must be >= 0.");

        if (request.SavedEmergencyFund < 0)
            throw new DomainValidationException("savedEmergencyFund must be >= 0.");

        var profile = await dbContext.HealthProfiles.FirstOrDefaultAsync(cancellationToken);
        if (profile is null)
        {
            profile = new HealthProfile { Id = Guid.NewGuid() };
            await dbContext.HealthProfiles.AddAsync(profile, cancellationToken);
        }

        profile.EssentialExpenses = decimal.Round(request.EssentialExpenses, 2);
        profile.SavedEmergencyFund = decimal.Round(request.SavedEmergencyFund, 2);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new HealthProfileDto(profile.Id, profile.EssentialExpenses, profile.SavedEmergencyFund);
    }
}
