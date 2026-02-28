using MiniErp.Api.Domain.Enums;

namespace MiniErp.Api.Contracts.Accounts;

public sealed record CashTxnListItem(
    Guid Id,
    Guid BranchId,
    CashTxnType Type,
    decimal Amount,
    string? Note,
    string RefType,
    Guid RefId,
    DateTimeOffset At
);

