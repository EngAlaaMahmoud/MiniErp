namespace MiniErp.Api.Contracts.Reports;

public sealed record PurchasesReportResponse(
    DateOnly From,
    DateOnly To,
    Guid BranchId,
    PurchasesReportSummary Summary,
    IReadOnlyList<PurchasesReportRow> Rows
);

public sealed record PurchasesReportSummary(
    int Count,
    decimal Total,
    decimal TaxTotal,
    decimal CashPaidTotal
);

public sealed record PurchasesReportRow(
    Guid PurchaseId,
    string PurchaseNo,
    DateTimeOffset At,
    string? SupplierName,
    decimal Total,
    decimal TaxTotal,
    decimal CashPaid
);

