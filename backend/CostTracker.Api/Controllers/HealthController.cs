using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CostTracker.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/health")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public ActionResult<object> Get()
    {
        return Ok(new { status = "ok" });
    }
}
