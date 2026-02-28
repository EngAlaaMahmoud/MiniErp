using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MiniErp.Api.Data;
using MiniErp.Api.Domain.Enums;

namespace MiniErp.Api.Security;

public sealed class PermissionService(AppDbContext db)
{
    public async Task<IReadOnlySet<string>> GetRolePermissionKeysAsync(Guid tenantId, UserRole role, CancellationToken ct)
    {
        var keys = await db.RolePermissions
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && x.Role == role)
            .Select(x => x.PermissionKey)
            .ToListAsync(ct);

        return new HashSet<string>(keys, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlySet<string>> GetEffectivePermissionKeysAsync(Guid tenantId, Guid userId, UserRole role, CancellationToken ct)
    {
        if (tenantId == Guid.Empty || userId == Guid.Empty)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var userKeys = await db.UserPermissions
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && x.UserId == userId)
            .Select(x => x.PermissionKey)
            .ToListAsync(ct);

        var profile = await db.UserPermissionProfiles
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && x.UserId == userId)
            .Select(x => new { x.InheritRole })
            .SingleOrDefaultAsync(ct);

        var inheritRole = profile?.InheritRole ?? true;
        if (!inheritRole)
        {
            return new HashSet<string>(userKeys, StringComparer.OrdinalIgnoreCase);
        }

        var roleKeys = await GetRolePermissionKeysAsync(tenantId, role, ct);
        var union = new HashSet<string>(roleKeys, StringComparer.OrdinalIgnoreCase);
        union.UnionWith(userKeys);
        return union;
    }

    public static UserRole? TryGetRole(ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue(ClaimTypes.Role) ?? user.FindFirstValue("role");
        return Enum.TryParse<UserRole>(raw, ignoreCase: true, out var role) ? role : null;
    }

    public static Guid TryGetUserId(ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        return Guid.TryParse(raw, out var userId) ? userId : Guid.Empty;
    }
}
