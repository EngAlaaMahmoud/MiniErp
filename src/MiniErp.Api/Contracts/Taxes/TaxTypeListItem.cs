namespace MiniErp.Api.Contracts.Taxes;

public sealed record TaxTypeListItem(
    Guid Id,
    string MainCode,
    string SubCode,
    string TaxType,
    string Description,
    bool IsActive
);

