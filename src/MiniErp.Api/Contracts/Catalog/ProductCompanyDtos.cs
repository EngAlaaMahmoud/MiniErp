namespace MiniErp.Api.Contracts.Catalog;

public sealed record ProductCompanyListItem(Guid Id, string Name, bool IsActive);

public sealed record CreateProductCompanyRequest(string Name, bool IsActive = true);

public sealed record UpdateProductCompanyRequest(string Name, bool IsActive = true);

