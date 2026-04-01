namespace Whitelabel.Infrastructure.Persistence.Entities;

public sealed class TenantEntity
{
    public string TenantId { get; set; } = "";
    public string TenantName { get; set; } = "";
    public string PrimaryColor { get; set; } = "";
    public string SecondaryColor { get; set; } = "";
    public string LogoUrl { get; set; } = "";
    public string Domain { get; set; } = "";
    public string EntraTenantId { get; set; } = "";

    public ICollection<TenantHostNameEntity> HostNames { get; set; } = new List<TenantHostNameEntity>();
    public ICollection<TenantEmailDomainEntity> EmailDomains { get; set; } = new List<TenantEmailDomainEntity>();
    public ICollection<TenantAdminEntity> TenantAdmins { get; set; } = new List<TenantAdminEntity>();
    public ICollection<TenantUserGrantEntity> UserGrants { get; set; } = new List<TenantUserGrantEntity>();
}
