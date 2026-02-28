namespace MiniErp.Api.Contracts.Reports;

public sealed record SalesReportResponse(
    DateOnly From,
    DateOnly To,
    Guid BranchId,
    SalesReportSummary Summary,
    IReadOnlyList<SalesReportRow> Rows
);

public sealed record SalesReportSummary(
    int Count,
    decimal Total,
    decimal TaxTotal
);

public sealed record SalesReportRow(
    Guid SaleId,
    string SaleNo,
    DateTimeOffset At,
    string? CustomerName,
    decimal Total,
    decimal TaxTotal
);

