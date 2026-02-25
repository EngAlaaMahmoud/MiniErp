namespace MiniErp.Api.Domain;

public sealed class Barcode : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ProductId { get; set; }
    public string Code { get; set; } = "";
}

