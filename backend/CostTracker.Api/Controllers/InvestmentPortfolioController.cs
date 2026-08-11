using CostTracker.Application.Contracts;
using CostTracker.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace CostTracker.Api.Controllers;

[ApiController]
[Route("api/investments")]
public class InvestmentPortfolioController(PortfolioManagementService portfolioManagementService) : ControllerBase
{
    [HttpGet("portfolio")]
    public async Task<ActionResult<InvestmentPortfolioDto>> GetPortfolio(CancellationToken cancellationToken)
        => Ok(await portfolioManagementService.GetPortfolioAsync(cancellationToken));

    [HttpPut("allocation")]
    public async Task<ActionResult<InvestmentPortfolioDto>> UpdateAllocation(
        [FromBody] UpdateInvestmentAllocationRequest request,
        CancellationToken cancellationToken)
        => Ok(await portfolioManagementService.UpdateAllocationAsync(request, cancellationToken));
}
