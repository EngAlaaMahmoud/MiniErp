using MiniErp.Api.Domain.Enums;

namespace MiniErp.Api.Domain;

public sealed class TaxLedgerEntry : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    public Guid BranchId { get; set; }
    public DateTimeOffset At { get; set; }

    public TaxLedgerType Type { get; set; }
    public Guid? TaxRateId { get; set; }
    public decimal TaxPercent { get; set; }
    public decimal Amount { get; set; }

    public string RefType { get; set; } = "";
    public Guid RefId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

