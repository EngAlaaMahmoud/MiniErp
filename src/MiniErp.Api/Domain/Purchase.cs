namespace MiniErp.Api.Domain;

public sealed class Purchase : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    public Guid BranchId { get; set; }
    public Guid DeviceId { get; set; }
    public Guid UserId { get; set; }

    public string Number { get; set; } = "";
    public DateTimeOffset At { get; set; }

    public Guid? SupplierId { get; set; }
    public string? SupplierName { get; set; }

    public decimal Total { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal CashPaid { get; set; }

    public string? Note { get; set; }
}
