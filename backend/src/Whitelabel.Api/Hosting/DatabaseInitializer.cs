using Microsoft.EntityFrameworkCore;
using Whitelabel.Infrastructure.Persistence;

namespace Whitelabel.Api.Hosting;

public sealed class DatabaseInitializer(IServiceScopeFactory scopeFactory, ILogger<DatabaseInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WhitelabelDbContext>();
        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();

        logger.LogInformation("Applying database migrations…");
        await db.Database.MigrateAsync(cancellationToken);
        await seeder.SeedIfEmptyAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
