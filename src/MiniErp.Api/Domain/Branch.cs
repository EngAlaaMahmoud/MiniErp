namespace MiniErp.Api.Domain;

public sealed class Branch : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = "";
}

