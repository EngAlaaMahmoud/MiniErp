namespace MiniErp.Api.Contracts.Auth;

public sealed record PinLoginRequest(
    string UserName,
    string Pin
);

