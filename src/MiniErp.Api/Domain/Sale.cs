using MiniErp.Api.Domain.Enums;

namespace MiniErp.Api.Domain;

public sealed class Sale : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public Guid DeviceId { get; set; }
    public Guid? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerTaxRegistrationNo { get; set; }
    public string? CustomerAddress { get; set; }
    public string Number { get; set; } = "";
    public DateTimeOffset At { get; set; }
    public decimal Total { get; set; }
    public decimal TaxTotal { get; set; }
    public SaleStatus Status { get; set; }

    // Base64-encoded SVG (UTF-8) for the invoice QR code.
    public string? QrCodeBase64 { get; set; }
}
