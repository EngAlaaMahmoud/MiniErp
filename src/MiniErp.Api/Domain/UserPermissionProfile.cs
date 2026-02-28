namespace MiniErp.Api.Domain;

public sealed class UserPermissionProfile : ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public bool InheritRole { get; set; } = true;
    public DateTimeOffset UpdatedAt { get; set; }
}

