namespace MiniErp.Api.Domain;

public interface ITenantScoped
{
    Guid TenantId { get; set; }
}

