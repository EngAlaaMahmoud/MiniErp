using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MiniErp.Api.IntegrationTests;

public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim("tenant_id", TestConstants.TenantId.ToString()),
            new Claim("device_id", TestConstants.DeviceId.ToString()),
            new Claim("sub", TestConstants.OwnerUserId.ToString()),
            new Claim(ClaimTypes.Role, "Owner")
        };

        var identity = new ClaimsIdentity(claims, authenticationType: "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
