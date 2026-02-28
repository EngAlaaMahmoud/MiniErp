namespace MiniErp.Api.Contracts.Purchases;

public sealed record CreatePurchaseResponse(Guid PurchaseId, string PurchaseNo, decimal Total);

