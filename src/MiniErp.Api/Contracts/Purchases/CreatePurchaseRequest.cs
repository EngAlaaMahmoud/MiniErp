using System.ComponentModel.DataAnnotations;

namespace MiniErp.Api.Contracts.Purchases;

public sealed class CreatePurchaseRequest
{
    public Guid BranchId { get; set; }
    public Guid? SupplierId { get; set; }
    public string? SupplierName { get; set; }

    [MinLength(1)]
    public List<CreatePurchaseLine> Items { get; set; } = [];

    public decimal CashPaid { get; set; }
    public string? Note { get; set; }
}

public sealed class CreatePurchaseLine
{
    public Guid ProductId { get; set; }
    public Guid? ProductUnitId { get; set; }
    public decimal Qty { get; set; }
    public decimal UnitCost { get; set; }
}
