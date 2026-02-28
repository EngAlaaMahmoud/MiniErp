namespace MiniErp.Api.Contracts.Catalog;

public sealed record ProductListItem(
    Guid Id,
    string Name,
    string? Sku,
    decimal Price,
    decimal Cost,
    Guid? CategoryId,
    Guid? TaxRateId,
    Guid? SalesTaxTypeId,
    decimal ReorderLevel,
    bool IsActive
);

public sealed record CreateProductRequest(
    string Name,
    string? Sku,
    decimal Price,
    decimal Cost,
    Guid? CategoryId,
    Guid? TaxRateId,
    Guid? SalesTaxTypeId,
    decimal ReorderLevel,
    bool IsActive
);

public sealed record UpdateProductRequest(
    string Name,
    string? Sku,
    decimal Price,
    decimal Cost,
    Guid? CategoryId,
    Guid? TaxRateId,
    Guid? SalesTaxTypeId,
    decimal ReorderLevel,
    bool IsActive
);

public sealed record AddBarcodeRequest(string Code);

public sealed record BarcodeLookupResponse(
    Guid ProductId,
    string Name,
    decimal Price,
    string Barcode
);
