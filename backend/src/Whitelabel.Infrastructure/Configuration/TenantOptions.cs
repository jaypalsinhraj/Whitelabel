using Whitelabel.Domain.Tenant;

namespace Whitelabel.Infrastructure.Configuration;

public sealed class TenantOptions
{
    public const string SectionName = "Tenants";

    public string DefaultTenantId { get; set; } = "fabrikam";
    public List<TenantConfigurationDto> Items { get; set; } = [];

    public IReadOnlyList<TenantConfiguration> GetConfigurations()
    {
        return Items.Select(i => i.ToDomain()).ToList();
    }
}

public sealed class TenantConfigurationDto
{
    public string TenantId { get; set; } = "";
    public string TenantName { get; set; } = "";
    public string PrimaryColor { get; set; } = "";
    public string SecondaryColor { get; set; } = "";
    public string LogoUrl { get; set; } = "";
    public string Domain { get; set; } = "";
    public string EntraTenantId { get; set; } = "";
    public List<string> HostNames { get; set; } = [];
    public List<string> EmailDomains { get; set; } = [];
    public List<string> TenantAdminObjectIds { get; set; } = [];

    public TenantConfiguration ToDomain() => new()
    {
        TenantId = TenantId,
        TenantName = TenantName,
        PrimaryColor = PrimaryColor,
        SecondaryColor = SecondaryColor,
        LogoUrl = LogoUrl,
        Domain = Domain,
        EntraTenantId = EntraTenantId,
    };
}
