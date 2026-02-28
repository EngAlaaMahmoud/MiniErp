namespace MiniErp.Api.Domain;

public sealed class Customer : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    public string Name { get; set; } = "";
    public string? Phone { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
}

