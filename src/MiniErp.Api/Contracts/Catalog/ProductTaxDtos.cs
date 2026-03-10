namespace MiniErp.Api.Contracts.Catalog;

public sealed record ProductTaxListItem(Guid Id, Guid SalesTaxTypeId);

public sealed record UpdateProductTaxesRequest(IReadOnlyList<Guid> SalesTaxTypeIds);

