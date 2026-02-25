namespace MiniErp.Api.Domain;

public sealed class Counter : ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = "";
    public long NextValue { get; set; }
}

