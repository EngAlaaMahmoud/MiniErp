namespace MiniErp.Api.Contracts.Reports;

public sealed record TrialBalanceResponse(
    DateOnly From,
    DateOnly To,
    Guid BranchId,
    TrialBalanceSummary Summary,
    IReadOnlyList<TrialBalanceRow> Rows
);

public sealed record TrialBalanceSummary(
    decimal TotalDebit,
    decimal TotalCredit
);

public sealed record TrialBalanceRow(
    Guid AccountId,
    string Code,
    string Name,
    decimal Debit,
    decimal Credit
);

