using MiniErp.Api.Domain.Enums;

namespace MiniErp.Api.Domain;

public sealed class CashTxn : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public CashTxnType Type { get; set; }
    public decimal Amount { get; set; }
    public string? Note { get; set; }
    public string RefType { get; set; } = "";
    public Guid RefId { get; set; }
    public DateTimeOffset At { get; set; }
}

