namespace MiniErp.Api.Infrastructure.Tenancy;

public sealed class HttpHeaderTenantProvider(IHttpContextAccessor httpContextAccessor) : ITenantProvider
{
    public Guid TenantId
    {
        get
        {
            var httpContext = httpContextAccessor.HttpContext;
            if (httpContext is null)
            {
                return Guid.Empty;
            }

            var claim = httpContext.User.FindFirst("tenant_id")?.Value;
            if (Guid.TryParse(claim, out var fromClaim) && fromClaim != Guid.Empty)
            {
                return fromClaim;
            }

            if (!httpContext.Request.Headers.TryGetValue("X-Tenant-Id", out var raw))
            {
                return Guid.Empty;
            }

            return Guid.TryParse(raw.ToString(), out var tenantId) ? tenantId : Guid.Empty;
        }
    }
}
