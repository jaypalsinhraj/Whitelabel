namespace Whitelabel.Infrastructure.Persistence.Entities;

public sealed class TenantUserGrantEntity
{
    public string TenantId { get; set; } = "";
    public string ObjectId { get; set; } = "";
    public TenantEntity Tenant { get; set; } = null!;
}
