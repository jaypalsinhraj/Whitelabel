using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Whitelabel.Infrastructure.Tenant;

namespace Whitelabel.Api.Controllers;

[ApiController]
[Route("admin")]
[Authorize]
public sealed class AdminController : ControllerBase
{
    [HttpGet("tenants")]
    public async Task<IActionResult> ListTenants([FromServices] TenantCatalog catalog, CancellationToken cancellationToken)
    {
        var oid = UserObjectId();
        if (!await catalog.IsApplicationAdminAsync(oid, cancellationToken))
        {
            return Forbid();
        }

        return Ok(await catalog.AllTenantDetailsAsync(cancellationToken));
    }

    [HttpPut("tenants/{tenantId}")]
    public async Task<IActionResult> UpdateTenant(string tenantId, [FromBody] UpdateTenantDto req, [FromServices] TenantCatalog catalog, CancellationToken cancellationToken)
    {
        var oid = UserObjectId();
        if (!await catalog.IsApplicationAdminAsync(oid, cancellationToken))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return BadRequest(new { message = "tenantId is required." });
        }

        var name = req.TenantName?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(new { message = "tenantName is required." });
        }

        static List<string> NormList(List<string>? items, bool lowerForDomain = false) =>
            (items ?? [])
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(s => lowerForDomain ? s.ToLowerInvariant() : s)
                .ToList();

        try
        {
            var updated = await catalog.UpdateTenantFullAsync(tenantId.Trim(), new UpdateTenantFullRequest(
                name,
                req.PrimaryColor?.Trim() ?? "#1a56db",
                req.SecondaryColor?.Trim() ?? "#0f3b99",
                req.LogoUrl?.Trim() ?? "",
                req.Domain?.Trim() ?? "",
                req.EntraTenantId?.Trim() ?? "",
                NormList(req.HostNames?.ToList()),
                NormList(req.EmailDomains?.ToList(), lowerForDomain: true),
                NormList(req.TenantAdminObjectIds?.ToList())), cancellationToken);

            return Ok(updated);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Tenant not found." });
        }
    }

    [HttpPost("tenants")]
    public async Task<IActionResult> CreateTenant([FromBody] CreateTenantDto req, [FromServices] TenantCatalog catalog, CancellationToken cancellationToken)
    {
        var oid = UserObjectId();
        if (!await catalog.IsApplicationAdminAsync(oid, cancellationToken))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(req.TenantId) || string.IsNullOrWhiteSpace(req.TenantName))
        {
            return BadRequest(new { message = "tenantId and tenantName are required." });
        }

        var created = await catalog.CreateTenantAsync(new CreateTenantRequest(
            req.TenantId.Trim(),
            req.TenantName.Trim(),
            req.PrimaryColor?.Trim() ?? "#1a56db",
            req.SecondaryColor?.Trim() ?? "#0f3b99",
            req.LogoUrl?.Trim() ?? "",
            req.Domain?.Trim() ?? "",
            req.EntraTenantId?.Trim() ?? "",
            req.HostNames ?? [],
            req.EmailDomains ?? [],
            req.TenantAdminObjectIds ?? []), cancellationToken);

        return Ok(created);
    }

    [HttpPut("tenants/{tenantId}/branding")]
    public async Task<IActionResult> UpdateBranding(string tenantId, [FromBody] UpdateBrandingDto req, [FromServices] TenantCatalog catalog, CancellationToken cancellationToken)
    {
        var oid = UserObjectId();
        if (!(await catalog.IsApplicationAdminAsync(oid, cancellationToken) || await catalog.IsTenantAdminAsync(tenantId, oid, cancellationToken)))
        {
            return Forbid();
        }

        var updated = await catalog.UpdateBrandingAsync(tenantId, new UpdateBrandingRequest(
            req.TenantName?.Trim(),
            req.PrimaryColor?.Trim(),
            req.SecondaryColor?.Trim(),
            req.LogoUrl?.Trim(),
            req.Domain?.Trim(),
            req.EntraTenantId?.Trim()), cancellationToken);

        return Ok(updated);
    }

    [HttpPost("tenants/{tenantId}/users")]
    public async Task<IActionResult> GrantUser(string tenantId, [FromBody] GrantUserAccessDto req, [FromServices] TenantCatalog catalog, CancellationToken cancellationToken)
    {
        var oid = UserObjectId();
        if (!(await catalog.IsApplicationAdminAsync(oid, cancellationToken) || await catalog.IsTenantAdminAsync(tenantId, oid, cancellationToken)))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(req.UserObjectId))
        {
            return BadRequest(new { message = "userObjectId is required." });
        }

        await catalog.GrantTenantUserAccessAsync(tenantId, req.UserObjectId.Trim(), cancellationToken);
        return Ok(new { message = "User access granted.", tenantId, userObjectId = req.UserObjectId.Trim() });
    }

    [HttpGet("tenants/{tenantId}/users")]
    public async Task<IActionResult> ListUsers(string tenantId, [FromServices] TenantCatalog catalog, CancellationToken cancellationToken)
    {
        var oid = UserObjectId();
        if (!(await catalog.IsApplicationAdminAsync(oid, cancellationToken) || await catalog.IsTenantAdminAsync(tenantId, oid, cancellationToken)))
        {
            return Forbid();
        }

        return Ok(new { tenantId, users = await catalog.ListTenantUsersAsync(tenantId, cancellationToken) });
    }

    private string? UserObjectId()
    {
        return User.FindFirstValue("oid") ??
               User.FindFirstValue("http://schemas.microsoft.com/identity/claims/objectidentifier");
    }
}

public sealed record CreateTenantDto(
    string TenantId,
    string TenantName,
    string? PrimaryColor,
    string? SecondaryColor,
    string? LogoUrl,
    string? Domain,
    string? EntraTenantId,
    List<string>? HostNames,
    List<string>? EmailDomains,
    List<string>? TenantAdminObjectIds);

public sealed record UpdateBrandingDto(
    string? TenantName,
    string? PrimaryColor,
    string? SecondaryColor,
    string? LogoUrl,
    string? Domain,
    string? EntraTenantId);

public sealed record UpdateTenantDto(
    string? TenantName,
    string? PrimaryColor,
    string? SecondaryColor,
    string? LogoUrl,
    string? Domain,
    string? EntraTenantId,
    List<string>? HostNames,
    List<string>? EmailDomains,
    List<string>? TenantAdminObjectIds);

public sealed record GrantUserAccessDto(string UserObjectId);
