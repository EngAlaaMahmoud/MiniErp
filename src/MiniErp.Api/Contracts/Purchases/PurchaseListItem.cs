namespace MiniErp.Api.Contracts.Purchases;

public sealed record PurchaseListItem(
    Guid Id,
    string Number,
    string? ExternalNumber,
    Guid BranchId,
    DateTimeOffset At,
    string? SupplierName,
    decimal Total,
    decimal CashPaid,
    decimal TaxTotal
);
