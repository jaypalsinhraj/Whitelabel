namespace Whitelabel.Domain.Tenant;

public sealed class TenantConfiguration
{
    public required string TenantId { get; init; }
    public required string TenantName { get; init; }
    public required string PrimaryColor { get; init; }
    public required string SecondaryColor { get; init; }
    public required string LogoUrl { get; init; }
    public required string Domain { get; init; }
    public required string EntraTenantId { get; init; }
}
