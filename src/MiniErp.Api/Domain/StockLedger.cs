using MiniErp.Api.Domain.Enums;

namespace MiniErp.Api.Domain;

public sealed class StockLedger : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public Guid ProductId { get; set; }
    public decimal QtyDelta { get; set; }
    public StockLedgerReason Reason { get; set; }
    public string RefType { get; set; } = "";
    public Guid RefId { get; set; }
    public DateTimeOffset At { get; set; }
}

