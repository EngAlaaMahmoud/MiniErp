namespace MiniErp.Api.Domain;

public sealed class Governorate
{
    public Guid Id { get; set; }
    public Guid CountryId { get; set; }
    public string Name { get; set; } = "";
    public string NameAr { get; set; } = "";
    public bool IsActive { get; set; } = true;

    public Country Country { get; set; } = null!;
}