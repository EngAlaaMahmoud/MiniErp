namespace MiniErp.Api.Domain;

public sealed class InventoryAdjustmentLine : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid AdjustmentId { get; set; }
    public Guid ProductId { get; set; }
    public decimal QtyDelta { get; set; }
}

