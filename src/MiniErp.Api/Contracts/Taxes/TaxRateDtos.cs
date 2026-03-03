using System.ComponentModel.DataAnnotations;

namespace MiniErp.Api.Contracts.Taxes;

public sealed record TaxRateListItem(Guid Id, string Name, decimal Percent, bool IsActive);

public sealed class CreateTaxRateRequest
{
    [Required]
    public string Name { get; set; } = "";

    public decimal Percent { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class UpdateTaxRateRequest
{
    [Required]
    public string Name { get; set; } = "";

    public decimal Percent { get; set; }
    public bool IsActive { get; set; }
}

public sealed record TaxSummaryResponse(
    DateTimeOffset From,
    DateTimeOffset To,
    Guid? BranchId,
    decimal SalesTax,
    decimal PurchaseTax,
    decimal NetTax
);
