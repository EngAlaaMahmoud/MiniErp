namespace MiniErp.Api.Domain;

public sealed class Product : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = "";
    public string? Sku { get; set; }
    public decimal Cost { get; set; }
    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;
}

