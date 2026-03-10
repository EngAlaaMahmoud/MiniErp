namespace MiniErp.Api.Contracts.Catalog;

public sealed record ProductCompanyListItem(
    Guid Id,
    string Name,
    string? Address,
    string? Fax,
    string? Email,
    string? Phone,
    string? Mobile,
    bool IsActive);

public sealed record CreateProductCompanyRequest(
    string Name,
    string? Address = null,
    string? Fax = null,
    string? Email = null,
    string? Phone = null,
    string? Mobile = null,
    bool IsActive = true);

public sealed record UpdateProductCompanyRequest(
    string Name,
    string? Address = null,
    string? Fax = null,
    string? Email = null,
    string? Phone = null,
    string? Mobile = null,
    bool IsActive = true);
