using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Whitelabel.Infrastructure.Configuration;
using Whitelabel.Infrastructure.Persistence.Entities;

namespace Whitelabel.Infrastructure.Persistence;

public sealed class DatabaseSeeder(
    WhitelabelDbContext db,
    IOptions<TenantOptions> tenantOptions,
    IOptions<AdminOptions> adminOptions,
    ILogger<DatabaseSeeder> logger)
{
    private readonly WhitelabelDbContext _db = db;
    private readonly TenantOptions _tenantOptions = tenantOptions.Value;
    private readonly AdminOptions _adminOptions = adminOptions.Value;

    public async Task SeedIfEmptyAsync(CancellationToken cancellationToken = default)
    {
        if (await _db.Tenants.AnyAsync(cancellationToken))
        {
            await EnsureApplicationAdminsFromConfigAsync(cancellationToken);
            return;
        }

        logger.LogInformation("Seeding database from configuration (initial empty catalog).");

        foreach (var dto in _tenantOptions.Items)
        {
            var entity = new TenantEntity
            {
                TenantId = dto.TenantId,
                TenantName = dto.TenantName,
                PrimaryColor = dto.PrimaryColor,
                SecondaryColor = dto.SecondaryColor,
                LogoUrl = dto.LogoUrl,
                Domain = dto.Domain,
                EntraTenantId = dto.EntraTenantId,
            };

            foreach (var h in dto.HostNames)
            {
                entity.HostNames.Add(new TenantHostNameEntity { HostName = h });
            }

            foreach (var d in dto.EmailDomains)
            {
                entity.EmailDomains.Add(new TenantEmailDomainEntity { Domain = d });
            }

            foreach (var oid in dto.TenantAdminObjectIds.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                entity.TenantAdmins.Add(new TenantAdminEntity { ObjectId = oid.Trim() });
            }

            _db.Tenants.Add(entity);
        }

        foreach (var oid in _adminOptions.ApplicationAdminObjectIds.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            _db.ApplicationAdmins.Add(new ApplicationAdminEntity { ObjectId = oid.Trim() });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureApplicationAdminsFromConfigAsync(CancellationToken cancellationToken)
    {
        foreach (var oid in _adminOptions.ApplicationAdminObjectIds.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            var trimmed = oid.Trim();
            if (!await _db.ApplicationAdmins.AnyAsync(a => a.ObjectId == trimmed, cancellationToken))
            {
                _db.ApplicationAdmins.Add(new ApplicationAdminEntity { ObjectId = trimmed });
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
