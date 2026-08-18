using CostTracker.Application.Investments.Dividends;
using CostTracker.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace CostTracker.Api.Controllers;

[ApiController]
[Route("api/investments")]
public sealed class InvestmentDividendsController(DividendService service) : ControllerBase
{
    [HttpGet("dividends")]
    public async Task<ActionResult<IReadOnlyList<DividendEventDto>>> GetEvents(
        [FromQuery] Guid? instrumentId,
        CancellationToken cancellationToken)
        => Ok(await service.GetEventsAsync(instrumentId, cancellationToken));

    [HttpGet("dividends/cash")]
    public async Task<ActionResult<DividendCashSummaryDto>> GetCash(CancellationToken cancellationToken)
        => Ok(await service.GetCashSummaryAsync(cancellationToken));

    [HttpPost("instruments/{instrumentId:guid}/dividends")]
    public async Task<ActionResult<DividendEventDto>> CreateEvent(
        Guid instrumentId,
        [FromBody] CreateDividendEventRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateEventAsync(instrumentId, request, cancellationToken);
        return Created($"/api/investments/dividends/{result.Id}", result);
    }

    [HttpDelete("dividends/{eventId:guid}")]
    public async Task<IActionResult> DeleteEvent(Guid eventId, CancellationToken cancellationToken)
    {
        await service.DeleteEventAsync(eventId, cancellationToken);
        return NoContent();
    }
}
