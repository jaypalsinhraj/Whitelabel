using Microsoft.AspNetCore.Mvc;
using Whitelabel.Api.Middleware;

namespace Whitelabel.Api.Controllers;

[ApiController]
[Route("tenant")]
public sealed class TenantController : ControllerBase
{
    [HttpGet]
    public IActionResult Get([FromServices] IHttpContextAccessor httpContextAccessor)
    {
        var tenant = httpContextAccessor.HttpContext?.GetCurrentTenant();
        if (tenant is null)
        {
            return NotFound(new { message = "Tenant could not be resolved." });
        }

        return Ok(new
        {
            tenantId = tenant.TenantId,
            tenantName = tenant.TenantName,
            primaryColor = tenant.PrimaryColor,
            secondaryColor = tenant.SecondaryColor,
            logoUrl = tenant.LogoUrl,
            domain = tenant.Domain,
            entraTenantId = tenant.EntraTenantId,
        });
    }
}
