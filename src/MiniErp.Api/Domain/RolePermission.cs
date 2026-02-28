using MiniErp.Api.Domain.Enums;

namespace MiniErp.Api.Domain;

public sealed class RolePermission : ITenantScoped
{
    public Guid TenantId { get; set; }
    public UserRole Role { get; set; }
    public string PermissionKey { get; set; } = "";
}

