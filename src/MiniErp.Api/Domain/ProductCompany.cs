namespace MiniErp.Api.Domain;

public sealed class ProductCompany : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    public string Name { get; set; } = "";
    public string? Address { get; set; }
    public string? Fax { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Mobile { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
}
