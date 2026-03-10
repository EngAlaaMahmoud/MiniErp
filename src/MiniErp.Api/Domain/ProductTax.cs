namespace MiniErp.Api.Domain;

public sealed class ProductTax : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    public Guid ProductId { get; set; }
    public Guid SalesTaxTypeId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

