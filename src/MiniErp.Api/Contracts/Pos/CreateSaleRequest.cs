namespace MiniErp.Api.Contracts.Pos;

public sealed record CreateSaleRequest(
    Guid BranchId,
    Guid? CustomerId,
    IReadOnlyList<SaleItemRequest> Items,
    IReadOnlyList<PaymentRequest> Payments,
    string? Note
);

public sealed record SaleItemRequest(
    Guid ProductId,
    Guid? ProductUnitId,
    decimal Qty,
    decimal UnitPrice,
    decimal Discount
);

public sealed record PaymentRequest(
    string Method,
    decimal Amount,
    string? ReferenceNo = null
);
