namespace Whitelabel.Infrastructure.Configuration;

public sealed class TenantResolutionOptions
{
    public const string SectionName = "TenantResolution";

    /// <summary>
    /// When false, the X-Tenant-Id header is ignored (recommended for production).
    /// </summary>
    public bool AllowTenantIdHeader { get; set; } = true;
}
