namespace MiniErp.Api.Domain;

public sealed class Device : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public string DeviceKey { get; set; } = "";
    public DateTimeOffset? LastSeenAt { get; set; }
}

