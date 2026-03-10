using System.ComponentModel.DataAnnotations;

namespace MiniErp.Api.Contracts.Taxes;

public sealed class UpdateTaxTypeRequest
{
    [Required]
    public string MainCode { get; set; } = "";

    [Required]
    public string SubCode { get; set; } = "";

    [Required]
    public string TaxType { get; set; } = "";

    [Required]
    public string Description { get; set; } = "";

    public decimal Percent { get; set; }

    public bool IsActive { get; set; }
}
