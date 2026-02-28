namespace MiniErp.Api.Contracts.Catalog;

public sealed record ProductUnitListItem(
    Guid Id,
    Guid ProductId,
    string Name,
    decimal Factor,
    bool IsDefault,
    bool IsActive
);

public sealed record CreateProductUnitRequest(
    string Name,
    decimal Factor,
    bool IsDefault,
    bool IsActive
);

public sealed record UpdateProductUnitRequest(
    string Name,
    decimal Factor,
    bool IsDefault,
    bool IsActive
);

