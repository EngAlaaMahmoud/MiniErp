namespace MiniErp.Api.Domain;

public sealed class StockBalance : ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public Guid ProductId { get; set; }
    public decimal Qty { get; set; }
}

