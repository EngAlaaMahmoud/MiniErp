namespace MiniErp.Api.Infrastructure.Tenancy;

public interface ITenantProvider
{
    Guid TenantId { get; }
}

