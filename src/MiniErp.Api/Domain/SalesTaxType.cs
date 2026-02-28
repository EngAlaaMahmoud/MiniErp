namespace MiniErp.Api.Domain;

public sealed class SalesTaxType : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    public string MainCode { get; set; } = ""; // e.g. T_1
    public string SubCode { get; set; } = "";  // e.g. V001
    public string TaxType { get; set; } = "";  // e.g. VAT / Tbl / WHT
    public string Description { get; set; } = "";

    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

