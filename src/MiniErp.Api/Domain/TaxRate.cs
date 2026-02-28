namespace MiniErp.Api.Domain;

public sealed class TaxRate : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    public string Name { get; set; } = ""; // e.g. VAT 14%
    public decimal Percent { get; set; }   // 0.14m

    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
}

