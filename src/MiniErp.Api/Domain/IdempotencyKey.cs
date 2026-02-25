namespace MiniErp.Api.Domain;

public sealed class IdempotencyKey : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid DeviceId { get; set; }
    public string Key { get; set; } = "";
    public string Endpoint { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public int? ResponseStatusCode { get; set; }
    public string? ResponseBody { get; set; }
}

