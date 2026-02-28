using MiniErp.Api.Domain.Enums;

namespace MiniErp.Api.Contracts.Admin;

public sealed record PermissionItem(string Key, string Group, string Name);

public sealed record RolePermissionsResponse(UserRole Role, IReadOnlyList<string> AllowedKeys);

public sealed record UpdateRolePermissionsRequest(IReadOnlyList<string> AllowedKeys);

