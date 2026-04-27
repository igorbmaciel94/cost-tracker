using CostTracker.Application.Contracts;
using CostTracker.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace CostTracker.Api.Controllers;

[ApiController]
[Route("api/months")]
public class MonthsController(MonthService monthService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MonthSummaryDto>>> GetMonths(CancellationToken ct)
        => Ok(await monthService.GetMonthsAsync(ct));

    [HttpPost("new")]
    public async Task<ActionResult<MonthSummaryDto>> CreateNewMonth(
        [FromBody] CreateMonthRequest? request,
        CancellationToken ct)
    {
        var result = await monthService.CreateNewMonthAsync(request?.ReferenceMonth, ct);
        return Created($"/api/months/{result.Id}", result);
    }

    [HttpPut("{monthId:guid}/salary")]
    public async Task<ActionResult<MonthSummaryDto>> UpdateSalary(
        Guid monthId,
        [FromBody] UpdateSalaryRequest request,
        CancellationToken ct)
        => Ok(await monthService.UpdateSalaryAsync(monthId, request.Salary, ct));
}
