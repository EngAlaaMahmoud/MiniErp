namespace MiniErp.Api.Domain;

public sealed class Tenant
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? Plan { get; set; }
    public string? TaxRegistrationNo { get; set; }
    public string? Address { get; set; }
}
