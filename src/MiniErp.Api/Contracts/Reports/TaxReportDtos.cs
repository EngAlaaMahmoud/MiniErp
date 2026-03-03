using MiniErp.Api.Domain.Enums;

namespace MiniErp.Api.Contracts.Reports;

public sealed record TaxReportResponse(
    DateOnly From,
    DateOnly To,
    Guid BranchId,
    TaxReportSummary Summary,
    IReadOnlyList<TaxReportRow> Rows
);

public sealed record TaxReportSummary(
    decimal InputTotal,
    decimal OutputTotal,
    decimal NetPayable
);

public sealed record TaxReportRow(
    TaxLedgerType Type,
    Guid? TaxRateId,
    string? TaxRateName,
    decimal TaxPercent,
    decimal Amount
);

