namespace MiniErp.Api.Domain;

public sealed class UnitMeasure : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    public string Name { get; set; } = "";
    public decimal Capacity { get; set; } = 1m;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
}
