namespace MiniErp.Api.Contracts.Accounts;

public sealed record ChartAccountListItem(
    Guid Id,
    string Code,
    string Name,
    int AccountType,
    Guid? ParentAccountId,
    bool IsPosting,
    bool IsActive
);

public sealed record CreateChartAccountRequest(
    string Code,
    string Name,
    int AccountType,
    Guid? ParentAccountId,
    bool IsPosting = true,
    bool IsActive = true
);
