namespace MiniErp.Api.Domain;

public sealed class Country
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string NameAr { get; set; } = "";
    public bool IsActive { get; set; } = true;

    public ICollection<Governorate> Governorates { get; set; } = [];
}