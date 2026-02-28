namespace MiniErp.Api.Domain;

using MiniErp.Api.Domain.Enums;

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

    public IdempotencyStatus Status { get; set; } = IdempotencyStatus.InProgress;
    public DateTimeOffset? LockedUntil { get; set; }
    public int AttemptCount { get; set; }
    public string? LastError { get; set; }

    public int? ResponseStatusCode { get; set; }
    public string? ResponseBody { get; set; }
}
