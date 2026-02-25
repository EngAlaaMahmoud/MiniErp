namespace MiniErp.Api.Contracts.Pos;

public sealed record CreateSaleResponse(
    Guid SaleId,
    string SaleNo,
    decimal Total
);

