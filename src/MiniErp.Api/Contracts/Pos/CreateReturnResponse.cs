namespace MiniErp.Api.Contracts.Pos;

public sealed record CreateReturnResponse(
    Guid ReturnId,
    string ReturnNo,
    decimal Total
);

