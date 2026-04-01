namespace Whitelabel.Infrastructure.Persistence.Entities;

public sealed class TenantEmailDomainEntity
{
    public int Id { get; set; }
    public string TenantId { get; set; } = "";
    public string Domain { get; set; } = "";
    public TenantEntity Tenant { get; set; } = null!;
}
