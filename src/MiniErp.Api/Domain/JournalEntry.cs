namespace MiniErp.Api.Domain;

public sealed class JournalEntry : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    public Guid BranchId { get; set; }
    public DateTimeOffset At { get; set; }

    public string SourceType { get; set; } = "";
    public Guid SourceId { get; set; }

    public string? Description { get; set; }

    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

