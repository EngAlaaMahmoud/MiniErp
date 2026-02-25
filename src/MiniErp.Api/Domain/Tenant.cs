namespace MiniErp.Api.Domain;

public sealed class Tenant
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? Plan { get; set; }
}

