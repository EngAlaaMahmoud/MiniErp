namespace MiniErp.Api.Contracts.Pos;

public sealed record CreateSaleRequest(
    Guid BranchId,
    IReadOnlyList<SaleItemRequest> Items,
    IReadOnlyList<PaymentRequest> Payments,
    string? Note
);

public sealed record SaleItemRequest(
    Guid ProductId,
    decimal Qty,
    decimal UnitPrice,
    decimal Discount
);

public sealed record PaymentRequest(
    string Method,
    decimal Amount
);

