using Whitelabel.Application.Tenant;
using Whitelabel.Domain.Tenant;

namespace Whitelabel.Infrastructure.Tenant;

public sealed class ConfigurationTenantResolver(TenantCatalog tenantCatalog) : ITenantResolver
{
    public Task<TenantConfiguration?> ResolveAsync(TenantResolutionContext context, CancellationToken cancellationToken = default)
    {
        return tenantCatalog.ResolveByHostOrHeaderAsync(context.Host, context.TenantHeader, cancellationToken);
    }
}
