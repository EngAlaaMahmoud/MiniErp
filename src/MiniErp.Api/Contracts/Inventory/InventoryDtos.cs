namespace MiniErp.Api.Contracts.Inventory;

public sealed record StockBalanceItem(
    Guid ProductId,
    string ProductName,
    decimal Qty
);

public sealed record CreateAdjustmentRequest(
    Guid BranchId,
    IReadOnlyList<CreateAdjustmentLine> Lines,
    string? Note
);

public sealed record CreateAdjustmentLine(
    Guid ProductId,
    decimal QtyDelta
);

public sealed record CreateAdjustmentResponse(
    Guid AdjustmentId,
    string AdjustmentNo
);

