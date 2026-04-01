using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Whitelabel.Infrastructure.Persistence;

/// <summary>
/// Used by EF Core CLI (<c>dotnet ef migrations</c>). Point at a local PostgreSQL instance or set env.
/// </summary>
public sealed class WhitelabelDbContextFactory : IDesignTimeDbContextFactory<WhitelabelDbContext>
{
    public WhitelabelDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<WhitelabelDbContext>();
        var cs = Environment.GetEnvironmentVariable("WHITELABEL_DESIGN_PG")
            ?? "Host=localhost;Database=whitelabel;Username=postgres;Password=postgres";
        optionsBuilder.UseNpgsql(cs);
        return new WhitelabelDbContext(optionsBuilder.Options);
    }
}
