namespace MiniErp.Api.Contracts.Catalog;

public sealed record CategoryListItem(Guid Id, string Name, Guid? ParentId, bool IsActive);

public sealed record CreateCategoryRequest(string Name, Guid? ParentId = null, bool IsActive = true);

public sealed record UpdateCategoryRequest(string Name, Guid? ParentId = null, bool IsActive = true);
