namespace MiniErp.Api.Domain;

public sealed class ProductUnit : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ProductId { get; set; }

    public string Name { get; set; } = "";
    public decimal Factor { get; set; } = 1m;

    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

