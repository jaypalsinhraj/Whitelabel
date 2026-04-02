using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Whitelabel.Domain.Tenant;
using Whitelabel.Infrastructure.Configuration;
using Whitelabel.Infrastructure.Persistence;
using Whitelabel.Infrastructure.Persistence.Entities;

namespace Whitelabel.Infrastructure.Tenant;

public sealed class TenantCatalog(
    WhitelabelDbContext db,
    IOptions<TenantOptions> tenantOptions,
    IOptions<AdminOptions> adminOptions)
{
    private readonly WhitelabelDbContext _db = db;
    private readonly string _defaultTenantId = tenantOptions.Value.DefaultTenantId;
    private readonly HashSet<string> _configAppAdminObjectIds = new(
        adminOptions.Value.ApplicationAdminObjectIds.Where(x => !string.IsNullOrWhiteSpace(x)),
        StringComparer.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<TenantConfiguration>> AllTenantConfigsAsync(CancellationToken cancellationToken = default)
    {
        var list = await _db.Tenants.AsNoTracking().ToListAsync(cancellationToken);
        return list.Select(ToConfiguration).ToList();
    }

    public async Task<IReadOnlyList<TenantAdminDetail>> AllTenantDetailsAsync(CancellationToken cancellationToken = default)
    {
        var list = await _db.Tenants.AsNoTracking()
            .Include(t => t.HostNames)
            .Include(t => t.EmailDomains)
            .Include(t => t.TenantAdmins)
            .OrderBy(t => t.TenantId)
            .ToListAsync(cancellationToken);
        return list.Select(ToAdminDetail).ToList();
    }

    public async Task<TenantConfiguration?> FindByIdAsync(string? tenantId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return null;
        }

        var entity = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.TenantId == tenantId, cancellationToken);
        return entity is null ? null : ToConfiguration(entity);
    }

    public async Task<TenantConfiguration?> ResolveByHostOrHeaderAsync(string host, string? tenantHeader, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(tenantHeader))
        {
            var h = tenantHeader.Trim();
            var byHeader = await _db.Tenants.AsNoTracking()
                .FirstOrDefaultAsync(t => t.TenantId.ToLower() == h.ToLower(), cancellationToken);
            if (byHeader is not null)
            {
                return ToConfiguration(byHeader);
            }
        }

        var hostNorm = host.Trim();
        var byHost = await _db.TenantHostNames.AsNoTracking()
            .Include(h => h.Tenant)
            .FirstOrDefaultAsync(h => h.HostName.ToLower() == hostNorm.ToLower(), cancellationToken);
        if (byHost?.Tenant is not null)
        {
            return ToConfiguration(byHost.Tenant);
        }

        var byDomain = await _db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Domain.ToLower() == hostNorm.ToLower(), cancellationToken);
        if (byDomain is not null)
        {
            return ToConfiguration(byDomain);
        }

        if (!string.IsNullOrWhiteSpace(_defaultTenantId))
        {
            var def = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.TenantId == _defaultTenantId, cancellationToken);
            if (def is not null)
            {
                return ToConfiguration(def);
            }
        }

        var first = await _db.Tenants.AsNoTracking().OrderBy(t => t.TenantId).FirstOrDefaultAsync(cancellationToken);
        return first is null ? null : ToConfiguration(first);
    }

    public async Task<bool> IsApplicationAdminAsync(string? objectId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(objectId))
        {
            return false;
        }

        if (_configAppAdminObjectIds.Contains(objectId))
        {
            return true;
        }

        return await _db.ApplicationAdmins.AsNoTracking().AnyAsync(a => a.ObjectId == objectId, cancellationToken);
    }

    public async Task<bool> IsTenantAdminAsync(string tenantId, string? objectId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(objectId))
        {
            return false;
        }

        return await _db.TenantAdmins.AsNoTracking()
            .AnyAsync(a => a.TenantId == tenantId && a.ObjectId == objectId, cancellationToken);
    }

    public async Task<bool> IsTenantAdminForRequestAsync(string? objectId, string? resolvedTenantId, string? mappedTenantId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(objectId))
        {
            return false;
        }

        foreach (var tenantId in new[] { resolvedTenantId, mappedTenantId })
        {
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                continue;
            }

            if (await IsTenantAdminAsync(tenantId, objectId, cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    public async Task<TenantConfiguration> CreateTenantAsync(CreateTenantRequest req, CancellationToken cancellationToken = default)
    {
        if (await _db.Tenants.AnyAsync(t => t.TenantId == req.TenantId, cancellationToken))
        {
            throw new InvalidOperationException("Tenant already exists.");
        }

        var entity = new TenantEntity
        {
            TenantId = req.TenantId,
            TenantName = req.TenantName,
            PrimaryColor = req.PrimaryColor,
            SecondaryColor = req.SecondaryColor,
            LogoUrl = req.LogoUrl,
            Domain = req.Domain,
            EntraTenantId = req.EntraTenantId,
        };

        foreach (var h in req.HostNames)
        {
            entity.HostNames.Add(new TenantHostNameEntity { HostName = h });
        }

        foreach (var d in req.EmailDomains)
        {
            entity.EmailDomains.Add(new TenantEmailDomainEntity { Domain = d });
        }

        foreach (var oid in req.TenantAdminObjectIds)
        {
            entity.TenantAdmins.Add(new TenantAdminEntity { ObjectId = oid });
        }

        _db.Tenants.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return ToConfiguration(entity);
    }

    public async Task<TenantConfiguration> UpdateBrandingAsync(string tenantId, UpdateBrandingRequest req, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Tenants.FirstOrDefaultAsync(t => t.TenantId == tenantId, cancellationToken);
        if (entity is null)
        {
            throw new KeyNotFoundException("Tenant not found.");
        }

        entity.TenantName = req.TenantName ?? entity.TenantName;
        entity.PrimaryColor = req.PrimaryColor ?? entity.PrimaryColor;
        entity.SecondaryColor = req.SecondaryColor ?? entity.SecondaryColor;
        entity.LogoUrl = req.LogoUrl ?? entity.LogoUrl;
        entity.Domain = req.Domain ?? entity.Domain;
        entity.EntraTenantId = req.EntraTenantId ?? entity.EntraTenantId;

        await _db.SaveChangesAsync(cancellationToken);
        return ToConfiguration(entity);
    }

    public async Task<TenantAdminDetail> UpdateTenantFullAsync(string tenantId, UpdateTenantFullRequest req, CancellationToken cancellationToken = default)
    {
        await _db.TenantHostNames.Where(h => h.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await _db.TenantEmailDomains.Where(d => d.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await _db.TenantAdmins.Where(a => a.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);

        var entity = await _db.Tenants.FirstOrDefaultAsync(t => t.TenantId == tenantId, cancellationToken);
        if (entity is null)
        {
            throw new KeyNotFoundException("Tenant not found.");
        }

        entity.TenantName = req.TenantName;
        entity.PrimaryColor = req.PrimaryColor;
        entity.SecondaryColor = req.SecondaryColor;
        entity.LogoUrl = req.LogoUrl;
        entity.Domain = req.Domain;
        entity.EntraTenantId = req.EntraTenantId;

        foreach (var h in req.HostNames)
        {
            _db.TenantHostNames.Add(new TenantHostNameEntity { TenantId = tenantId, HostName = h });
        }

        foreach (var d in req.EmailDomains)
        {
            _db.TenantEmailDomains.Add(new TenantEmailDomainEntity { TenantId = tenantId, Domain = d });
        }

        foreach (var oid in req.TenantAdminObjectIds)
        {
            _db.TenantAdmins.Add(new TenantAdminEntity { TenantId = tenantId, ObjectId = oid });
        }

        await _db.SaveChangesAsync(cancellationToken);

        var reloaded = await _db.Tenants.AsNoTracking()
            .Include(t => t.HostNames)
            .Include(t => t.EmailDomains)
            .Include(t => t.TenantAdmins)
            .FirstAsync(t => t.TenantId == tenantId, cancellationToken);
        return ToAdminDetail(reloaded);
    }

    public async Task GrantTenantUserAccessAsync(string tenantId, string userObjectId, CancellationToken cancellationToken = default)
    {
        var exists = await _db.Tenants.AnyAsync(t => t.TenantId == tenantId, cancellationToken);
        if (!exists)
        {
            throw new KeyNotFoundException("Tenant not found.");
        }

        if (!await _db.TenantUserGrants.AnyAsync(g => g.TenantId == tenantId && g.ObjectId == userObjectId, cancellationToken))
        {
            _db.TenantUserGrants.Add(new TenantUserGrantEntity { TenantId = tenantId, ObjectId = userObjectId });
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> HasTenantAccessAsync(string tenantId, string? objectId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(objectId))
        {
            return false;
        }

        var grant = await _db.TenantUserGrants.AsNoTracking()
            .AnyAsync(g => g.TenantId == tenantId && g.ObjectId == objectId, cancellationToken);
        if (grant)
        {
            return true;
        }

        return await IsTenantAdminAsync(tenantId, objectId, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> ListTenantUsersAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        return await _db.TenantUserGrants.AsNoTracking()
            .Where(g => g.TenantId == tenantId)
            .Select(g => g.ObjectId)
            .ToListAsync(cancellationToken);
    }

    public async Task<string?> MapUserToTenantIdByClaimsAsync(string? tenantClaimTid, string? emailOrUpn, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(tenantClaimTid))
        {
            var byTid = await _db.Tenants.AsNoTracking()
                .FirstOrDefaultAsync(t => t.EntraTenantId.ToLower() == tenantClaimTid.ToLower(), cancellationToken);
            if (byTid is not null)
            {
                return byTid.TenantId;
            }
        }

        if (string.IsNullOrWhiteSpace(emailOrUpn) || !emailOrUpn.Contains('@', StringComparison.Ordinal))
        {
            return null;
        }

        var domain = emailOrUpn.Split('@')[^1].Trim();

        var byEmailDomain = await _db.TenantEmailDomains.AsNoTracking()
            .Include(d => d.Tenant)
            .FirstOrDefaultAsync(d => d.Domain.ToLower() == domain.ToLower(), cancellationToken);
        if (byEmailDomain?.Tenant is not null)
        {
            return byEmailDomain.Tenant.TenantId;
        }

        var byTenantDomain = await _db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Domain.ToLower() == domain.ToLower(), cancellationToken);
        return byTenantDomain?.TenantId;
    }

    private static TenantConfiguration ToConfiguration(TenantEntity e) => new()
    {
        TenantId = e.TenantId,
        TenantName = e.TenantName,
        PrimaryColor = e.PrimaryColor,
        SecondaryColor = e.SecondaryColor,
        LogoUrl = e.LogoUrl,
        Domain = e.Domain,
        EntraTenantId = e.EntraTenantId,
    };

    private static TenantAdminDetail ToAdminDetail(TenantEntity e) => new(
        e.TenantId,
        e.TenantName,
        e.PrimaryColor,
        e.SecondaryColor,
        e.LogoUrl,
        e.Domain,
        e.EntraTenantId,
        e.HostNames.Select(h => h.HostName).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
        e.EmailDomains.Select(d => d.Domain).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
        e.TenantAdmins.Select(a => a.ObjectId).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList());
}

public sealed record TenantAdminDetail(
    string TenantId,
    string TenantName,
    string PrimaryColor,
    string SecondaryColor,
    string LogoUrl,
    string Domain,
    string EntraTenantId,
    IReadOnlyList<string> HostNames,
    IReadOnlyList<string> EmailDomains,
    IReadOnlyList<string> TenantAdminObjectIds);

public sealed record UpdateTenantFullRequest(
    string TenantName,
    string PrimaryColor,
    string SecondaryColor,
    string LogoUrl,
    string Domain,
    string EntraTenantId,
    List<string> HostNames,
    List<string> EmailDomains,
    List<string> TenantAdminObjectIds);

public sealed record CreateTenantRequest(
    string TenantId,
    string TenantName,
    string PrimaryColor,
    string SecondaryColor,
    string LogoUrl,
    string Domain,
    string EntraTenantId,
    List<string> HostNames,
    List<string> EmailDomains,
    List<string> TenantAdminObjectIds);

public sealed record UpdateBrandingRequest(
    string? TenantName,
    string? PrimaryColor,
    string? SecondaryColor,
    string? LogoUrl,
    string? Domain,
    string? EntraTenantId);
