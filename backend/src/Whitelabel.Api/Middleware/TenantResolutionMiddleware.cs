using Microsoft.Extensions.Options;
using Whitelabel.Application.Tenant;
using Whitelabel.Domain.Tenant;
using Whitelabel.Infrastructure.Configuration;

namespace Whitelabel.Api.Middleware;

public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        ITenantResolver tenantResolver,
        IOptionsSnapshot<TenantResolutionOptions> tenantResolutionOptions)
    {
        var host = context.Request.Host.Host;
        string? header = null;
        if (tenantResolutionOptions.Value.AllowTenantIdHeader &&
            context.Request.Headers.TryGetValue("X-Tenant-Id", out var h))
        {
            header = h.ToString();
        }

        var tenant = await tenantResolver.ResolveAsync(new TenantResolutionContext(host, header));
        if (tenant is not null)
        {
            context.Items[HttpContextItemKey] = tenant;
        }

        await next(context);
    }

    public const string HttpContextItemKey = "Whitelabel.Tenant";
}

public static class TenantResolutionMiddlewareExtensions
{
    public static TenantConfiguration? GetCurrentTenant(this HttpContext context)
    {
        if (context.Items.TryGetValue(TenantResolutionMiddleware.HttpContextItemKey, out var t) &&
            t is TenantConfiguration tenant)
        {
            return tenant;
        }

        return null;
    }
}
