using Whitelabel.Domain.Tenant;

namespace Whitelabel.Application.Tenant;

public interface ITenantResolver
{
    Task<TenantConfiguration?> ResolveAsync(TenantResolutionContext context, CancellationToken cancellationToken = default);
}
