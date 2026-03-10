using System.ComponentModel.DataAnnotations;

namespace MiniErp.Api.Contracts.Parties;

public sealed record CustomerListItem(
    Guid Id,
    string Name,
    string? Phone,
    string? TaxRegistrationNo,
    Guid? CustomerTypeId,
    string? CustomerTypeName,
    Guid? CountryId,
    string? CountryName,
    Guid? GovernorateId,
    string? GovernorateName,
    string? City,
    string? BuildingNo,
    string? Floor,
    string? Apartment,
    string? StreetName,
    string? PostalCode,
    string? Address,
    bool IsActive);

public sealed class CreateCustomerRequest
{
    [Required]
    public string Name { get; set; } = "";

    public string? Phone { get; set; }
    public string? TaxRegistrationNo { get; set; }
    public Guid? CustomerTypeId { get; set; }
    public Guid? CountryId { get; set; }
    public Guid? GovernorateId { get; set; }

    // allow sending free-text governorate for non-Egypt countries
    public string? GovernorateText { get; set; }

    public string? City { get; set; }
    public string? BuildingNo { get; set; }
    public string? Floor { get; set; }
    public string? Apartment { get; set; }
    public string? StreetName { get; set; }
    public string? PostalCode { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class UpdateCustomerRequest
{
    [Required]
    public string Name { get; set; } = "";

    public string? Phone { get; set; }
    public string? TaxRegistrationNo { get; set; }
    public Guid? CustomerTypeId { get; set; }
    public Guid? CountryId { get; set; }
    public Guid? GovernorateId { get; set; }

    // allow free-text governorate
    public string? GovernorateText { get; set; }

    public string? City { get; set; }
    public string? BuildingNo { get; set; }
    public string? Floor { get; set; }
    public string? Apartment { get; set; }
    public string? StreetName { get; set; }
    public string? PostalCode { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; }
}

public sealed record CountryOption(Guid Id, string Name, string NameAr);

public sealed record GovernorateOption(Guid Id, Guid CountryId, string Name, string NameAr);

public sealed record CustomerTypeOption(Guid Id, string Name, string NameAr);
