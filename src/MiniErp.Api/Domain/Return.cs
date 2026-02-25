namespace MiniErp.Api.Domain;

public sealed class Return : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public Guid DeviceId { get; set; }
    public Guid OrigSaleId { get; set; }
    public string Number { get; set; } = "";
    public DateTimeOffset At { get; set; }
    public decimal Total { get; set; }
    public string RefundMethod { get; set; } = "Cash";
}

