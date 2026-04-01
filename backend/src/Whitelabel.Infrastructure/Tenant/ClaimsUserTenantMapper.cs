using System.Security.Claims;
using Whitelabel.Application.Tenant;

namespace Whitelabel.Infrastructure.Tenant;

public sealed class ClaimsUserTenantMapper(TenantCatalog tenantCatalog) : IUserTenantMapper
{
    public Task<string?> MapToTenantIdAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        var email =
            user.FindFirstValue(ClaimTypes.Email) ??
            user.FindFirstValue("preferred_username") ??
            user.FindFirstValue("emails");

        var tid = user.FindFirstValue("tid");
        return tenantCatalog.MapUserToTenantIdByClaimsAsync(tid, email, cancellationToken);
    }
}
