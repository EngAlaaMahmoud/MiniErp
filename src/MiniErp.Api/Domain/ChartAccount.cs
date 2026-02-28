using MiniErp.Api.Domain.Enums;

namespace MiniErp.Api.Domain;

public sealed class ChartAccount : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public AccountType AccountType { get; set; }

    public Guid? ParentAccountId { get; set; }

    public bool IsPosting { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

