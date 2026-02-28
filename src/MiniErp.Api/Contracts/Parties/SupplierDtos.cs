using System.ComponentModel.DataAnnotations;

namespace MiniErp.Api.Contracts.Parties;

public sealed record SupplierListItem(Guid Id, string Name, string? Phone, bool IsActive);

public sealed class CreateSupplierRequest
{
    [Required]
    public string Name { get; set; } = "";

    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class UpdateSupplierRequest
{
    [Required]
    public string Name { get; set; } = "";

    public string? Phone { get; set; }
    public bool IsActive { get; set; }
}

