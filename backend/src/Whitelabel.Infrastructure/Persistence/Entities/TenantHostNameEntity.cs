namespace Whitelabel.Infrastructure.Persistence.Entities;

public sealed class TenantHostNameEntity
{
    public int Id { get; set; }
    public string TenantId { get; set; } = "";
    public string HostName { get; set; } = "";
    public TenantEntity Tenant { get; set; } = null!;
}
