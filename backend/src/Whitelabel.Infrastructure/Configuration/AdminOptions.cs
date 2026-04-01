namespace Whitelabel.Infrastructure.Configuration;

public sealed class AdminOptions
{
    public const string SectionName = "Admin";
    public List<string> ApplicationAdminObjectIds { get; set; } = [];
}
