namespace MiniErp.Api.Domain;

public sealed class InventoryAdjustment : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public Guid DeviceId { get; set; }
    public Guid UserId { get; set; }
    public string Number { get; set; } = "";
    public string? Note { get; set; }
    public DateTimeOffset At { get; set; }
}

