using CostTracker.Api.Contracts;
using CostTracker.Domain.Entities;
using CostTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CostTracker.Api.Controllers;

[ApiController]
[Route("api/financial-health/profile")]
public class FinancialHealthController(CostTrackerDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<HealthProfileDto>> GetProfile(CancellationToken cancellationToken)
    {
        var profile = await dbContext.HealthProfiles.FirstOrDefaultAsync(cancellationToken);
        if (profile is null)
            return Ok(new HealthProfileDto(Guid.Empty, 0, 0));

        return Ok(new HealthProfileDto(profile.Id, profile.EssentialExpenses, profile.SavedEmergencyFund));
    }

    [HttpPut]
    public async Task<ActionResult<HealthProfileDto>> UpsertProfile(
        [FromBody] UpdateHealthProfileRequest request,
        CancellationToken cancellationToken)
    {
        if (request.EssentialExpenses < 0)
            return BadRequest("essentialExpenses must be >= 0.");

        if (request.SavedEmergencyFund < 0)
            return BadRequest("savedEmergencyFund must be >= 0.");

        var profile = await dbContext.HealthProfiles.FirstOrDefaultAsync(cancellationToken);
        if (profile is null)
        {
            profile = new HealthProfile { Id = Guid.NewGuid() };
            await dbContext.HealthProfiles.AddAsync(profile, cancellationToken);
        }

        profile.EssentialExpenses = decimal.Round(request.EssentialExpenses, 2);
        profile.SavedEmergencyFund = decimal.Round(request.SavedEmergencyFund, 2);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new HealthProfileDto(profile.Id, profile.EssentialExpenses, profile.SavedEmergencyFund));
    }
}
