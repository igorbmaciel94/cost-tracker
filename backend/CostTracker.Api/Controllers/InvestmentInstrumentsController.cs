using CostTracker.Application.Contracts;
using CostTracker.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace CostTracker.Api.Controllers;

[ApiController]
[Route("api/investments/instruments")]
public class InvestmentInstrumentsController(PortfolioManagementService portfolioManagementService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<InvestmentInstrumentDto>>> GetInstruments(
        [FromQuery] bool includeArchived = false,
        CancellationToken cancellationToken = default)
        => Ok(await portfolioManagementService.GetInstrumentsAsync(includeArchived, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<InvestmentInstrumentDetailDto>> GetInstrument(
        Guid id,
        CancellationToken cancellationToken)
        => Ok(await portfolioManagementService.GetInstrumentAsync(id, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<InvestmentInstrumentDetailDto>> CreateInstrument(
        [FromBody] CreateInvestmentInstrumentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await portfolioManagementService.CreateInstrumentAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetInstrument), new { id = result.Instrument.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<InvestmentInstrumentDetailDto>> UpdateInstrument(
        Guid id,
        [FromBody] UpdateInvestmentInstrumentRequest request,
        CancellationToken cancellationToken)
        => Ok(await portfolioManagementService.UpdateInstrumentAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/archive")]
    public async Task<ActionResult<InvestmentInstrumentDetailDto>> ArchiveInstrument(
        Guid id,
        [FromQuery] long? expectedVersion,
        CancellationToken cancellationToken)
        => Ok(await portfolioManagementService.ArchiveInstrumentAsync(id, expectedVersion, cancellationToken));

    [HttpGet("{id:guid}/transactions")]
    public async Task<ActionResult<IReadOnlyList<InvestmentTransactionDto>>> GetTransactions(
        Guid id,
        CancellationToken cancellationToken)
        => Ok(await portfolioManagementService.GetTransactionsAsync(id, cancellationToken));

    [HttpPost("{id:guid}/transactions")]
    public async Task<ActionResult<InvestmentInstrumentDetailDto>> CreateTransaction(
        Guid id,
        [FromBody] CreateInvestmentTransactionRequest request,
        CancellationToken cancellationToken)
        => Ok(await portfolioManagementService.CreateTransactionAsync(id, request, cancellationToken));

    [HttpGet("{id:guid}/manual-valuations")]
    public async Task<ActionResult<IReadOnlyList<ManualValuationDto>>> GetManualValuations(
        Guid id,
        CancellationToken cancellationToken)
        => Ok(await portfolioManagementService.GetManualValuationsAsync(id, cancellationToken));

    [HttpPost("{id:guid}/manual-valuations")]
    public async Task<ActionResult<InvestmentInstrumentDetailDto>> CreateManualValuation(
        Guid id,
        [FromBody] CreateManualValuationRequest request,
        CancellationToken cancellationToken)
        => Ok(await portfolioManagementService.CreateManualValuationAsync(id, request, cancellationToken));
}
