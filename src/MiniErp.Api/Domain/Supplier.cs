namespace MiniErp.Api.Domain;

public sealed class Supplier : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    public string Name { get; set; } = "";
    public string? Phone { get; set; }
    public string? TaxRegistrationNo { get; set; }

    // store selected lookup ids so UI can pre-select dropdowns
    public Guid? CountryId { get; set; }
    public Guid? GovernorateId { get; set; }

    // resolved / stored display values (kept for address building and display)
    public string? Country { get; set; }
    public string? Governorate { get; set; }

    public string? City { get; set; }
    public string? BuildingNo { get; set; }
    public string? Floor { get; set; }
    public string? Apartment { get; set; }
    public string? StreetName { get; set; }
    public string? PostalCode { get; set; }
    public string? Address { get; set; }

    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}