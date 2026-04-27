using CostTracker.Application.Contracts;
using CostTracker.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace CostTracker.Api.Controllers;

[ApiController]
[Route("api/months/{monthId:guid}/budget")]
public class BudgetController(BudgetService budgetService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<BudgetResponseDto>> GetBudget(Guid monthId, CancellationToken ct)
        => Ok(await budgetService.GetBudgetAsync(monthId, ct));

    [HttpPost("categories")]
    public async Task<ActionResult<BudgetResponseDto>> CreateCategory(
        Guid monthId,
        [FromBody] CreateCategoryRequest request,
        CancellationToken ct)
        => Ok(await budgetService.CreateCategoryAsync(monthId, request, ct));

    [HttpPut("categories/{categoryId:guid}")]
    public async Task<ActionResult<BudgetResponseDto>> UpdateCategory(
        Guid monthId,
        Guid categoryId,
        [FromBody] UpdateCategoryRequest request,
        CancellationToken ct)
        => Ok(await budgetService.UpdateCategoryAsync(monthId, categoryId, request, ct));

    [HttpDelete("categories/{categoryId:guid}")]
    public async Task<ActionResult<BudgetResponseDto>> DeleteCategory(
        Guid monthId,
        Guid categoryId,
        CancellationToken ct)
        => Ok(await budgetService.DeleteCategoryAsync(monthId, categoryId, ct));
}
