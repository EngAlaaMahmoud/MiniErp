using MiniErp.Api.Domain.Enums;

namespace MiniErp.Api.Contracts.Reports;

public sealed record StockLedgerReportResponse(
    DateOnly From,
    DateOnly To,
    Guid BranchId,
    Guid? ProductId,
    StockLedgerReportSummary Summary,
    IReadOnlyList<StockLedgerReportRow> Rows
);

public sealed record StockLedgerReportSummary(
    decimal InTotal,
    decimal OutTotal,
    decimal NetTotal
);

public sealed record StockLedgerReportRow(
    DateTimeOffset At,
    Guid ProductId,
    string ProductName,
    decimal QtyDelta,
    StockLedgerReason Reason,
    string RefType,
    Guid RefId
);

