namespace MiniErp.Api.Contracts.Pos;

public sealed record SaleListItem(
    Guid Id,
    string Number,
    Guid BranchId,
    DateTimeOffset At,
    string? CustomerName,
    decimal Total,
    decimal TaxTotal
);

