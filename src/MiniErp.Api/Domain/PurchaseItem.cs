namespace MiniErp.Api.Domain;

public sealed class PurchaseItem : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    public Guid PurchaseId { get; set; }
    public Guid ProductId { get; set; }

    public decimal Qty { get; set; }
    public decimal QtyBase { get; set; }
    public Guid? ProductUnitId { get; set; }
    public string? UnitName { get; set; }
    public decimal UnitFactor { get; set; } = 1m;
    public decimal UnitCost { get; set; }
    public decimal LineTotal { get; set; }
    public decimal TaxPercent { get; set; }
    public decimal TaxAmount { get; set; }
}
