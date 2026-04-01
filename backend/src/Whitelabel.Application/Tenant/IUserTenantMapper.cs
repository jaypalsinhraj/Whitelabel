using System.Security.Claims;

namespace Whitelabel.Application.Tenant;

public interface IUserTenantMapper
{
    Task<string?> MapToTenantIdAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default);
}
