namespace MiniErp.Api.Contracts.Catalog;

public sealed record CategoryListItem(Guid Id, string Name, bool IsActive);

public sealed record CreateCategoryRequest(string Name, bool IsActive);

public sealed record UpdateCategoryRequest(string Name, bool IsActive);

