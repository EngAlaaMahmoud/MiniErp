namespace MiniErp.Api.Domain;

public sealed class UserPermission : ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string PermissionKey { get; set; } = "";
}

