namespace MiniErp.Api.Domain;

public sealed class CustomerType
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string NameAr { get; set; } = "";
    public bool IsActive { get; set; } = true;
}