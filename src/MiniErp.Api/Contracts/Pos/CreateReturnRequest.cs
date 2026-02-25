namespace MiniErp.Api.Contracts.Pos;

public sealed record CreateReturnRequest(
    Guid BranchId,
    Guid OrigSaleId,
    IReadOnlyList<ReturnItemRequest> Items,
    string RefundMethod,
    string? Note
);

public sealed record ReturnItemRequest(
    Guid ProductId,
    decimal Qty,
    decimal UnitPrice,
    decimal Discount
);

