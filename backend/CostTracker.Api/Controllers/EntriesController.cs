using CostTracker.Application.Contracts;
using CostTracker.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace CostTracker.Api.Controllers;

[ApiController]
[Route("api/months/{monthId:guid}/entries")]
public class EntriesController(EntryService entryService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<EntriesResponseDto>> GetEntries(Guid monthId, CancellationToken ct)
        => Ok(await entryService.GetEntriesAsync(monthId, ct));

    [HttpPost]
    public async Task<ActionResult<EntriesResponseDto>> CreateEntry(
        Guid monthId,
        [FromBody] CreateEntryRequest request,
        CancellationToken ct)
        => Ok(await entryService.CreateEntryAsync(monthId, request, ct));

    [HttpPut("{entryId:guid}")]
    public async Task<ActionResult<EntriesResponseDto>> UpdateEntry(
        Guid monthId,
        Guid entryId,
        [FromBody] UpdateEntryRequest request,
        CancellationToken ct)
        => Ok(await entryService.UpdateEntryAsync(monthId, entryId, request, ct));

    [HttpDelete("{entryId:guid}")]
    public async Task<ActionResult<EntriesResponseDto>> DeleteEntry(
        Guid monthId,
        Guid entryId,
        CancellationToken ct)
        => Ok(await entryService.DeleteEntryAsync(monthId, entryId, ct));
}
