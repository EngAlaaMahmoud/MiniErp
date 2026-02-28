namespace MiniErp.Api.Contracts.Admin;

public sealed record UserPermissionsResponse(Guid UserId, bool InheritRole, IReadOnlyList<string> AllowedKeys);

public sealed record UpdateUserPermissionsRequest(bool InheritRole, IReadOnlyList<string> AllowedKeys);

