using CostTracker.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace CostTracker.Api.Controllers;

[ApiController]
[Route("api/months/{monthId:guid}/ai-analysis")]
public class AiAnalysisController(AiAnalysisService svc) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Generate(Guid monthId, CancellationToken ct)
    {
        var pdfBytes = await svc.GenerateAsync(monthId, ct);
        return File(pdfBytes, "application/pdf", $"analise-{monthId}.pdf");
    }
}
