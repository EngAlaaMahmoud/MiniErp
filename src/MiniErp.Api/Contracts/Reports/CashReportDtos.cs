using MiniErp.Api.Domain.Enums;

namespace MiniErp.Api.Contracts.Reports;

public sealed record CashReportResponse(
    DateOnly From,
    DateOnly To,
    Guid BranchId,
    CashReportSummary Summary,
    IReadOnlyList<CashReportRow> Rows
);

public sealed record CashReportSummary(
    decimal NetTotal,
    decimal InTotal,
    decimal OutTotal
);

public sealed record CashReportRow(
    Guid Id,
    DateTimeOffset At,
    CashTxnType Type,
    decimal Amount,
    string? Note,
    string RefType,
    Guid RefId
);

