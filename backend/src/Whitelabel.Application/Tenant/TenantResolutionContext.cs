namespace Whitelabel.Application.Tenant;

public sealed record TenantResolutionContext(string Host, string? TenantHeader);
