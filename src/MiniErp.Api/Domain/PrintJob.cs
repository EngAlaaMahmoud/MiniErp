using MiniErp.Api.Domain.Enums;

namespace MiniErp.Api.Domain;

public sealed class PrintJob : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public Guid DeviceId { get; set; }
    public string RefType { get; set; } = "";
    public Guid RefId { get; set; }
    public string PayloadJson { get; set; } = "";
    public PrintJobStatus Status { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset? NextRetryAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

