namespace MiniErp.Api.Domain;

public sealed class Product : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = "";
    public string? Sku { get; set; }
    public string? BrandName { get; set; }
    public string? Description { get; set; }
    public decimal Cost { get; set; }
    public decimal Price { get; set; }
    public decimal DefaultDiscount { get; set; }

    public Guid? CategoryId { get; set; }
    public Guid? TaxRateId { get; set; }
    public Guid? SalesTaxTypeId { get; set; }
    public decimal ReorderLevel { get; set; }
    public bool IsActive { get; set; } = true;
}
