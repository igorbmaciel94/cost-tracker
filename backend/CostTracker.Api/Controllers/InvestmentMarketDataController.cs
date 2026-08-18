using CostTracker.Application.Investments.MarketData;
using CostTracker.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace CostTracker.Api.Controllers;

[ApiController]
[Route("api/investments/market-data")]
public sealed class InvestmentMarketDataController(InvestmentMarketDataService service) : ControllerBase
{
    [HttpGet("status")]
    public async Task<ActionResult<MarketDataStatusDto>> GetStatus(CancellationToken cancellationToken)
        => Ok(await service.GetStatusAsync(cancellationToken));

    [HttpPost("refresh")]
    public async Task<ActionResult<MarketDataStatusDto>> Refresh(CancellationToken cancellationToken)
        => Ok(await service.RefreshAsync(cancellationToken, retryStaleSources: true));

    [HttpGet("instruments/{instrumentId:guid}/mappings")]
    public async Task<ActionResult<IReadOnlyList<MarketInstrumentMappingDto>>> GetMappings(
        Guid instrumentId,
        CancellationToken cancellationToken)
        => Ok(await service.GetMappingsAsync(instrumentId, cancellationToken));

    [HttpPut("instruments/{instrumentId:guid}/mappings")]
    public async Task<ActionResult<MarketInstrumentMappingDto>> UpsertMapping(
        Guid instrumentId,
        [FromBody] UpsertMarketInstrumentMappingRequest request,
        CancellationToken cancellationToken)
        => Ok(await service.UpsertMappingAsync(instrumentId, request, cancellationToken));

    [HttpPost("instruments/{instrumentId:guid}/manual-quote")]
    public async Task<IActionResult> RecordManualQuote(
        Guid instrumentId,
        [FromBody] ManualMarketQuoteRequest request,
        CancellationToken cancellationToken)
    {
        await service.RecordManualQuoteAsync(instrumentId, request, cancellationToken);
        return NoContent();
    }
}
