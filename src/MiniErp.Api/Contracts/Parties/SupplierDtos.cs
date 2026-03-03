using System.ComponentModel.DataAnnotations;

namespace MiniErp.Api.Contracts.Parties;

public sealed record SupplierListItem(
    Guid Id,
    string Name,
    string? Phone,
    string? TaxRegistrationNo,
    string? Country,
    string? Governorate,
    string? City,
    string? BuildingNo,
    string? Floor,
    string? Apartment,
    string? StreetName,
    string? PostalCode,
    string? Address,
    bool IsActive);

public sealed class CreateSupplierRequest
{
    [Required]
    public string Name { get; set; } = "";

    public string? Phone { get; set; }
    public string? TaxRegistrationNo { get; set; }
    public string? Country { get; set; }
    public string? Governorate { get; set; }
    public string? City { get; set; }
    public string? BuildingNo { get; set; }
    public string? Floor { get; set; }
    public string? Apartment { get; set; }
    public string? StreetName { get; set; }
    public string? PostalCode { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class UpdateSupplierRequest
{
    [Required]
    public string Name { get; set; } = "";

    public string? Phone { get; set; }
    public string? TaxRegistrationNo { get; set; }
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
}
