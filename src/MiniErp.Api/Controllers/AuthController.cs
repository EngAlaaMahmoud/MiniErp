using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniErp.Api.Contracts.Auth;
using MiniErp.Api.Data;
using MiniErp.Api.Services;

namespace MiniErp.Api.Controllers;

[ApiController]
[Route("auth")]
public sealed class AuthController(AppDbContext db, PinHasher pinHasher, JwtTokenService tokenService) : ControllerBase
{
    [HttpPost("pin")]
    public async Task<IActionResult> PinLogin([FromBody] PinLoginRequest request, CancellationToken ct)
    {
        var tenantId = db.TenantId;
        if (tenantId == Guid.Empty)
        {
            return BadRequest(new { error = "TENANT_REQUIRED", header = "X-Tenant-Id" });
        }

        if (!Request.Headers.TryGetValue("X-Device-Id", out var rawDeviceId) ||
            !Guid.TryParse(rawDeviceId.ToString(), out var deviceId) ||
            deviceId == Guid.Empty)
        {
            return BadRequest(new { error = "DEVICE_REQUIRED", header = "X-Device-Id" });
        }

        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Pin))
        {
            return BadRequest(new { error = "INVALID_CREDENTIALS" });
        }

        var user = await db.Users.SingleOrDefaultAsync(
            x => x.Name == request.UserName && x.IsActive,
            ct);

        if (user is null || !pinHasher.Verify(request.Pin, user.PinHash))
        {
            return Unauthorized(new { error = "INVALID_CREDENTIALS" });
        }

        var token = tokenService.CreateAccessToken(tenantId, deviceId, user);
        return Ok(new PinLoginResponse(token, user.Id, user.Name, user.Role, tenantId, deviceId));
    }
}

