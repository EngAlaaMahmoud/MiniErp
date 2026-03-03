namespace MiniErp.Api.Domain;

public sealed class JournalEntryLine : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    public Guid JournalEntryId { get; set; }
    public Guid AccountId { get; set; }

    public decimal Debit { get; set; }
    public decimal Credit { get; set; }

    public Guid? CustomerId { get; set; }
    public Guid? SupplierId { get; set; }
    public string? Note { get; set; }
}

