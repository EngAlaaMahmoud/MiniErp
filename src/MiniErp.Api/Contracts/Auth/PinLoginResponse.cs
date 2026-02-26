using MiniErp.Api.Domain.Enums;

namespace MiniErp.Api.Contracts.Auth;

public sealed record PinLoginResponse(
    string AccessToken,
    Guid UserId,
    string UserName,
    UserRole Role,
    Guid TenantId,
    Guid DeviceId
);

