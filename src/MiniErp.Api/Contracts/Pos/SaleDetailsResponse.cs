namespace MiniErp.Api.Contracts.Pos;

public sealed record SaleDetailsResponse(
    Guid SaleId,
    string SaleNo,
    Guid BranchId,
    DateTimeOffset At,
    decimal Total,
    IReadOnlyList<SaleDetailsItem> Items,
    IReadOnlyList<SaleDetailsPayment> Payments
);

public sealed record SaleDetailsItem(
    Guid ProductId,
    string ProductName,
    Guid? ProductUnitId,
    string? UnitName,
    decimal UnitFactor,
    decimal Qty,
    decimal UnitPrice,
    decimal Discount,
    decimal LineTotal
);

public sealed record SaleDetailsPayment(
    string Method,
    decimal Amount,
    string? ReferenceNo
);
