namespace MiniErp.Api.Domain;

public sealed class Payment : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SaleId { get; set; }
    public string Method { get; set; } = "";
    public decimal Amount { get; set; }
    public DateTimeOffset PaidAt { get; set; }
    public string? ReferenceNo { get; set; }
    public string? Note { get; set; }
}
