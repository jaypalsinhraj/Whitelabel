using Microsoft.AspNetCore.Mvc;

namespace Whitelabel.Api.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { status = "Healthy", timestamp = DateTime.UtcNow });
}
