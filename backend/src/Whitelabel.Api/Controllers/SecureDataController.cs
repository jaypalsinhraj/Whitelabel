using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Whitelabel.Api.Middleware;
using Whitelabel.Application.Tenant;
using Whitelabel.Infrastructure.Tenant;

namespace Whitelabel.Api.Controllers;

[ApiController]
[Route("secure-data")]
[Authorize]
public sealed class SecureDataController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromServices] IUserTenantMapper userTenantMapper,
        [FromServices] TenantCatalog tenantCatalog,
        CancellationToken cancellationToken)
    {
        var tenant = HttpContext.GetCurrentTenant();
        var mappedTenantId = await userTenantMapper.MapToTenantIdAsync(User, cancellationToken);
        var oid = User.FindFirstValue("oid") ??
                  User.FindFirstValue("http://schemas.microsoft.com/identity/claims/objectidentifier");
        var isAppAdmin = await tenantCatalog.IsApplicationAdminAsync(oid, cancellationToken);
        var isTenantAdmin = await tenantCatalog.IsTenantAdminForRequestAsync(oid, tenant?.TenantId, mappedTenantId, cancellationToken);
        var accessTenantId = mappedTenantId ?? tenant?.TenantId ?? string.Empty;
        var hasAccess = isAppAdmin ||
                        isTenantAdmin ||
                        mappedTenantId is not null ||
                        await tenantCatalog.HasTenantAccessAsync(accessTenantId, oid, cancellationToken);

        if (mappedTenantId is not null && !hasAccess)
        {
            return Forbid();
        }

        return Ok(new
        {
            message = "Authenticated access to multi-tenant API.",
            resolvedBrandingTenantId = tenant?.TenantId,
            userMappedTenantId = mappedTenantId,
            roles = new
            {
                isApplicationAdmin = isAppAdmin,
                isTenantAdmin,
            },
            claims = new
            {
                tid = User.FindFirstValue("tid"),
                oid,
                email = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("preferred_username"),
                preferred_username = User.FindFirstValue("preferred_username"),
            },
        });
    }
}
