using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniErp.Api.Contracts.Admin;
using MiniErp.Api.Data;
using MiniErp.Api.Domain;
using MiniErp.Api.Domain.Enums;
using MiniErp.Api.Security;
using MiniErp.Api.Services;

namespace MiniErp.Api.Controllers;

[ApiController]
[Route("admin")]
[Authorize]
public sealed class AdminController(AppDbContext db, PinHasher pinHasher, PermissionService permissionService) : ControllerBase
{
    [HttpGet("permissions")]
    [Authorize(Roles = "Owner,Manager")]
    [RequirePermission(PermissionKeys.AdminPermissions)]
    public ActionResult<IReadOnlyList<PermissionItem>> ListPermissions()
        => Ok(PermissionCatalog.All.Select(x => new PermissionItem(x.Key, x.Group, x.Name)).ToList());

    [HttpGet("my-permissions")]
    public async Task<ActionResult<IReadOnlyList<string>>> MyPermissions(CancellationToken ct)
    {
        var tenantId = db.TenantId;
        if (tenantId == Guid.Empty)
        {
            return BadRequest(new { error = "TENANT_REQUIRED" });
        }

        var role = PermissionService.TryGetRole(User) ?? UserRole.Cashier;
        var userId = PermissionService.TryGetUserId(User);
        if (userId == Guid.Empty)
        {
            return BadRequest(new { error = "USER_REQUIRED" });
        }

        var keys = await permissionService.GetEffectivePermissionKeysAsync(tenantId, userId, role, ct);
        return Ok(keys.OrderBy(x => x).ToArray());
    }

    [HttpGet("role-permissions")]
    [Authorize(Roles = "Owner")]
    [RequirePermission(PermissionKeys.AdminPermissions)]
    public async Task<ActionResult<RolePermissionsResponse>> GetRolePermissions([FromQuery] UserRole role, CancellationToken ct)
    {
        var tenantId = db.TenantId;
        if (tenantId == Guid.Empty)
        {
            return BadRequest(new { error = "TENANT_REQUIRED" });
        }

        var keys = await permissionService.GetRolePermissionKeysAsync(tenantId, role, ct);
        return Ok(new RolePermissionsResponse(role, keys.OrderBy(x => x).ToArray()));
    }

    [HttpPut("role-permissions")]
    [Authorize(Roles = "Owner")]
    [RequirePermission(PermissionKeys.AdminPermissions)]
    public async Task<IActionResult> UpdateRolePermissions([FromQuery] UserRole role, [FromBody] UpdateRolePermissionsRequest request, CancellationToken ct)
    {
        var tenantId = db.TenantId;
        if (tenantId == Guid.Empty)
        {
            return BadRequest(new { error = "TENANT_REQUIRED" });
        }

        var allowed = (request.AllowedKeys ?? Array.Empty<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (allowed.Any(x => !PermissionCatalog.IsKnown(x)))
        {
            return BadRequest(new { error = "UNKNOWN_PERMISSION" });
        }

        var current = await db.RolePermissions
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && x.Role == role)
            .ToListAsync(ct);

        db.RolePermissions.RemoveRange(current);
        db.RolePermissions.AddRange(allowed.Select(k => new RolePermission
        {
            TenantId = tenantId,
            Role = role,
            PermissionKey = k
        }));

        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("users")]
    [Authorize(Roles = "Owner,Manager")]
    [RequirePermission(PermissionKeys.AdminPermissions)]
    public async Task<ActionResult<IReadOnlyList<UserAdminListItem>>> ListUsers(CancellationToken ct)
    {
        var tenantId = db.TenantId;
        if (tenantId == Guid.Empty)
        {
            return BadRequest(new { error = "TENANT_REQUIRED" });
        }

        var items = await db.Users
            .OrderBy(x => x.Name)
            .Take(1000)
            .Select(x => new UserAdminListItem(x.Id, x.Name, x.Role, x.IsActive, x.CreatedAt))
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpGet("user-permissions")]
    [Authorize(Roles = "Owner,Manager")]
    [RequirePermission(PermissionKeys.AdminPermissions)]
    public async Task<ActionResult<UserPermissionsResponse>> GetUserPermissions([FromQuery] Guid userId, CancellationToken ct)
    {
        var tenantId = db.TenantId;
        if (tenantId == Guid.Empty)
        {
            return BadRequest(new { error = "TENANT_REQUIRED" });
        }

        if (userId == Guid.Empty)
        {
            return BadRequest(new { error = "INVALID_USER" });
        }

        var exists = await db.Users.AnyAsync(x => x.Id == userId, ct);
        if (!exists)
        {
            return NotFound(new { error = "NOT_FOUND" });
        }

        var inheritRole = await db.UserPermissionProfiles
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && x.UserId == userId)
            .Select(x => (bool?)x.InheritRole)
            .SingleOrDefaultAsync(ct) ?? true;

        var allowed = await db.UserPermissions
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && x.UserId == userId)
            .Select(x => x.PermissionKey)
            .OrderBy(x => x)
            .ToListAsync(ct);

        return Ok(new UserPermissionsResponse(userId, inheritRole, allowed));
    }

    [HttpPut("user-permissions")]
    [Authorize(Roles = "Owner,Manager")]
    [RequirePermission(PermissionKeys.AdminPermissions)]
    public async Task<IActionResult> UpdateUserPermissions([FromQuery] Guid userId, [FromBody] UpdateUserPermissionsRequest request, CancellationToken ct)
    {
        var tenantId = db.TenantId;
        if (tenantId == Guid.Empty)
        {
            return BadRequest(new { error = "TENANT_REQUIRED" });
        }

        if (userId == Guid.Empty)
        {
            return BadRequest(new { error = "INVALID_USER" });
        }

        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null)
        {
            return NotFound(new { error = "NOT_FOUND" });
        }

        var allowed = (request.AllowedKeys ?? Array.Empty<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (allowed.Any(x => !PermissionCatalog.IsKnown(x)))
        {
            return BadRequest(new { error = "UNKNOWN_PERMISSION" });
        }

        var profile = await db.UserPermissionProfiles
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.UserId == userId, ct);

        if (profile is null)
        {
            db.UserPermissionProfiles.Add(new UserPermissionProfile
            {
                TenantId = tenantId,
                UserId = userId,
                InheritRole = request.InheritRole,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }
        else
        {
            profile.InheritRole = request.InheritRole;
            profile.UpdatedAt = DateTimeOffset.UtcNow;
        }

        var current = await db.UserPermissions
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && x.UserId == userId)
            .ToListAsync(ct);

        db.UserPermissions.RemoveRange(current);
        db.UserPermissions.AddRange(allowed.Select(k => new UserPermission { TenantId = tenantId, UserId = userId, PermissionKey = k }));

        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("users")]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        var tenantId = db.TenantId;
        if (tenantId == Guid.Empty)
        {
            return BadRequest(new { error = "TENANT_REQUIRED" });
        }

        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Pin))
        {
            return BadRequest(new { error = "INVALID_REQUEST" });
        }

        var name = request.Name.Trim();
        var exists = await db.Users.AnyAsync(x => x.Name == name, ct);
        if (exists)
        {
            return Conflict(new { error = "DUPLICATE_NAME" });
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            PinHash = pinHasher.HashPin(request.Pin.Trim()),
            Role = request.Role,
            IsActive = request.IsActive,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        return Created("", new UserAdminListItem(user.Id, user.Name, user.Role, user.IsActive, user.CreatedAt));
    }

    [HttpPut("users/{id:guid}")]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> UpdateUser([FromRoute] Guid id, [FromBody] UpdateUserRequest request, CancellationToken ct)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new { error = "INVALID_ID" });
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { error = "NAME_REQUIRED" });
        }

        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (user is null)
        {
            return NotFound(new { error = "NOT_FOUND" });
        }

        var name = request.Name.Trim();
        var duplicate = await db.Users.AnyAsync(x => x.Id != id && x.Name == name, ct);
        if (duplicate)
        {
            return Conflict(new { error = "DUPLICATE_NAME" });
        }

        user.Name = name;
        user.Role = request.Role;
        user.IsActive = request.IsActive;

        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("users/{id:guid}/reset-pin")]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> ResetPin([FromRoute] Guid id, [FromBody] ResetPinRequest request, CancellationToken ct)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new { error = "INVALID_ID" });
        }

        if (string.IsNullOrWhiteSpace(request.Pin))
        {
            return BadRequest(new { error = "PIN_REQUIRED" });
        }

        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (user is null)
        {
            return NotFound(new { error = "NOT_FOUND" });
        }

        user.PinHash = pinHasher.HashPin(request.Pin.Trim());
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
