using System.ComponentModel.DataAnnotations;

namespace MiniErp.Api.Contracts.Purchases;

public sealed class CreatePurchaseRequest
{
    public Guid BranchId { get; set; }
    public DateTimeOffset? At { get; set; }

    // Vendor invoice/reference number (as printed on the supplier invoice).
    public string? ExternalNumber { get; set; }

    public Guid? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public string? SupplierTaxRegistrationNo { get; set; }
    public string? SupplierAddress { get; set; }

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
