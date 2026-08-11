using CostTracker.Application.Investments.MarketData;
using CostTracker.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace CostTracker.Api.Controllers;

[ApiController]
[Route("api/investments/portfolio/valuation")]
public sealed class InvestmentValuationController(InvestmentMarketDataService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ValuedPortfolioDto>> Get(CancellationToken cancellationToken)
        => Ok(await service.GetPortfolioValuationAsync(cancellationToken));
}
