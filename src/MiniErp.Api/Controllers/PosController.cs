using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using MiniErp.Api.Contracts.Pos;
using MiniErp.Api.Data;
using MiniErp.Api.Domain;
using MiniErp.Api.Domain.Enums;
using MiniErp.Api.Infrastructure.Inventory;
using MiniErp.Api.Infrastructure.Accounting;
using MiniErp.Api.Infrastructure.Qr;
using MiniErp.Api.Infrastructure.Tax;
using MiniErp.Api.Security;
using MiniErp.Api.Services;
using Npgsql;

namespace MiniErp.Api.Controllers;

[ApiController]
[Route("pos")]
[Authorize]
public sealed class PosController(AppDbContext db, IdempotencyService idempotencyService, IConfiguration configuration) : ControllerBase
{
    [HttpGet("sales")]
    [RequirePermission(PermissionKeys.SalesView)]
    public async Task<ActionResult<IReadOnlyList<SaleListItem>>> ListSales(
        [FromQuery] Guid branchId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 200,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        [FromQuery] Guid? customerId = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        if (branchId == Guid.Empty)
        {
            return BadRequest(new { error = "BRANCH_REQUIRED" });
        }

        if (page < 1)
        {
            page = 1;
        }

        pageSize = pageSize switch
        {
            < 1 => 50,
            > 500 => 500,
            _ => pageSize
        };

        if (customerId == Guid.Empty)
        {
            customerId = null;
        }

        if (to is not null && from is not null && to.Value < from.Value)
        {
            return BadRequest(new { error = "INVALID_DATE_RANGE" });
        }

        var q = db.Sales
            .AsNoTracking()
            .Where(x => x.BranchId == branchId);

        if (from is not null)
        {
            var fromAt = new DateTimeOffset(from.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            q = q.Where(x => x.At >= fromAt);
        }

        if (to is not null)
        {
            var toAt = new DateTimeOffset(to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            q = q.Where(x => x.At < toAt);
        }

        if (customerId is not null)
        {
            q = q.Where(x => x.CustomerId == customerId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(x => EF.Functions.Like(x.Number, $"%{s}%") || (x.CustomerName != null && x.CustomerName.Contains(s)));
        }

        var items = await q
            .OrderByDescending(x => x.At)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new SaleListItem(x.Id, x.Number, x.BranchId, x.At, x.CustomerName, x.Total, x.TaxTotal))
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpGet("sales/{saleId:guid}")]
    [RequirePermission(PermissionKeys.SalesView)]
    public async Task<IActionResult> GetSale([FromRoute] Guid saleId, CancellationToken ct)
    {
        if (saleId == Guid.Empty)
        {
            return BadRequest(new { error = "INVALID_ID" });
        }

        var sale = await db.Sales.SingleOrDefaultAsync(x => x.Id == saleId, ct);
        if (sale is null)
        {
            return NotFound(new { error = "NOT_FOUND" });
        }

        var items = await db.SaleItems
            .Where(x => x.SaleId == saleId)
            .Join(db.Products, si => si.ProductId, p => p.Id, (si, p) => new { si, p })
            .OrderBy(x => x.p.Name)
            .Select(x => new SaleDetailsItem(
                x.si.ProductId,
                x.p.Name,
                x.si.ProductUnitId,
                x.si.UnitName,
                x.si.UnitFactor == 0 ? 1m : x.si.UnitFactor,
                x.si.Qty,
                x.si.UnitPrice,
                x.si.Discount,
                x.si.LineTotal))
            .ToListAsync(ct);

        var payments = await db.Payments
            .Where(x => x.SaleId == saleId)
            .Select(x => new SaleDetailsPayment(x.Method, x.Amount, x.ReferenceNo))
            .ToListAsync(ct);

        return Ok(new SaleDetailsResponse(
            sale.Id,
            sale.Number,
            sale.BranchId,
            sale.At,
            sale.Total,
            items,
            payments,
            TaxTotal: sale.TaxTotal,
            CustomerName: sale.CustomerName,
            CustomerTaxRegistrationNo: sale.CustomerTaxRegistrationNo,
            CustomerAddress: sale.CustomerAddress,
            QrCodeBase64: sale.QrCodeBase64));
    }

    [HttpGet("sales/by-number/{saleNo}")]
    [RequirePermission(PermissionKeys.SalesView)]
    public async Task<IActionResult> GetSaleByNumber([FromRoute] string saleNo, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(saleNo))
        {
            return BadRequest(new { error = "SALE_NO_REQUIRED" });
        }

        var sale = await db.Sales.SingleOrDefaultAsync(x => x.Number == saleNo, ct);
        if (sale is null)
        {
            return NotFound(new { error = "NOT_FOUND" });
        }

        return await GetSale(sale.Id, ct);
    }

    [HttpPost("sales")]
    [RequirePermission(PermissionKeys.SalesCreate)]
    public async Task<IActionResult> CreateSale([FromBody] CreateSaleRequest request, CancellationToken ct)
    {
        var tenantId = db.TenantId;
        if (tenantId == Guid.Empty)
        {
            return BadRequest(new { error = "TENANT_REQUIRED", header = "X-Tenant-Id" });
        }

        var deviceClaim = User.FindFirst("device_id")?.Value;
        if (!Guid.TryParse(deviceClaim, out var deviceId) || deviceId == Guid.Empty)
        {
            if (!Request.Headers.TryGetValue("X-Device-Id", out var rawDeviceId) ||
                !Guid.TryParse(rawDeviceId.ToString(), out deviceId) ||
                deviceId == Guid.Empty)
            {
                return BadRequest(new { error = "DEVICE_REQUIRED", header = "X-Device-Id" });
            }
        }

        if (!Request.Headers.TryGetValue("Idempotency-Key", out var rawKey) ||
            string.IsNullOrWhiteSpace(rawKey.ToString()))
        {
            return BadRequest(new { error = "IDEMPOTENCY_KEY_REQUIRED", header = "Idempotency-Key" });
        }

        var key = rawKey.ToString().Trim();
        var endpoint = "POST:/pos/sales";
        var requestHash = IdempotencyService.HashRequest(request);
        var ttlMinutes = configuration.GetValue("Idempotency:PosTtlMinutes", 3);
        var ttl = TimeSpan.FromMinutes(Math.Clamp(ttlMinutes, 1, 30));

        return await idempotencyService.ExecuteAsync(
            db,
            tenantId,
            deviceId,
            endpoint,
            key,
            requestHash,
            async (operationId, innerCt) =>
            {
                var now = DateTimeOffset.UtcNow;

                var existingSale = await db.Sales
                    .AsNoTracking()
                    .SingleOrDefaultAsync(x => x.Id == operationId, innerCt);
                if (existingSale is not null)
                {
                    return (StatusCodes.Status201Created, (object)new CreateSaleResponse(existingSale.Id, existingSale.Number, existingSale.Total));
                }

                if (request.BranchId == Guid.Empty)
                {
                    return (StatusCodes.Status400BadRequest, (object)new { error = "BRANCH_REQUIRED" });
                }

                if (request.Items.Count == 0)
                {
                    return (StatusCodes.Status400BadRequest, (object)new { error = "ITEMS_REQUIRED" });
                }

                Guid? customerId = null;
                string? customerName = null;
                string? customerTaxRegistrationNo = null;
                string? customerAddress = null;
                if (request.CustomerId is not null && request.CustomerId.Value != Guid.Empty)
                {
                    var customer = await db.Customers.SingleOrDefaultAsync(x => x.Id == request.CustomerId.Value && x.IsActive, innerCt);
                    if (customer is null)
                    {
                        return (StatusCodes.Status400BadRequest, (object)new { error = "INVALID_CUSTOMER" });
                    }

                    customerId = customer.Id;
                    customerName = customer.Name;
                    customerTaxRegistrationNo = customer.TaxRegistrationNo;
                    customerAddress = BuildPartyAddress(
                        customer.Country,
                        customer.Governorate,
                        customer.City,
                        customer.BuildingNo,
                        customer.Floor,
                        customer.Apartment,
                        customer.StreetName,
                        customer.PostalCode,
                        customer.Address);
                }
                else
                {
                    customerName = string.IsNullOrWhiteSpace(request.CustomerName) ? null : request.CustomerName.Trim();
                    customerTaxRegistrationNo = string.IsNullOrWhiteSpace(request.CustomerTaxRegistrationNo) ? null : request.CustomerTaxRegistrationNo.Trim();
                    customerAddress = string.IsNullOrWhiteSpace(request.CustomerAddress) ? null : request.CustomerAddress.Trim();
                }

                var productIds = request.Items.Select(x => x.ProductId).Distinct().ToArray();
                var products = await db.Products
                    .Where(x => productIds.Contains(x.Id))
                    .Select(x => new { x.Id, x.Cost, x.IsActive, x.TaxRateId })
                    .ToListAsync(innerCt);

                if (products.Count != productIds.Length)
                {
                    return (StatusCodes.Status400BadRequest, (object)new { error = "INVALID_PRODUCT" });
                }

                if (products.Any(x => !x.IsActive))
                {
                    return (StatusCodes.Status400BadRequest, (object)new { error = "INACTIVE_PRODUCT" });
                }

                if (products.Any(x => x.TaxRateId == null))
                {
                    return (StatusCodes.Status400BadRequest, (object)new { error = "PRODUCT_TAX_NOT_SET" });
                }

                var unitIds = request.Items.Where(x => x.ProductUnitId != null).Select(x => x.ProductUnitId!.Value).Distinct().ToArray();
                var units = unitIds.Length == 0
                    ? new List<ProductUnit>()
                    : await db.ProductUnits.Where(x => unitIds.Contains(x.Id)).ToListAsync(innerCt);

                if (units.Count != unitIds.Length)
                {
                    return (StatusCodes.Status400BadRequest, (object)new { error = "INVALID_UNIT" });
                }

                var unitById = units.ToDictionary(x => x.Id, x => x);

                var lines = request.Items.Select(x =>
                {
                    var factor = 1m;
                    string? unitName = null;
                    if (x.ProductUnitId != null)
                    {
                        var unit = unitById[x.ProductUnitId.Value];
                        if (!unit.IsActive || unit.ProductId != x.ProductId || unit.Factor <= 0)
                        {
                            return (Ok: false, Item: x, LineTotal: 0m, QtyBase: 0m, UnitFactor: 0m, UnitName: (string?)null);
                        }

                        factor = unit.Factor;
                        unitName = unit.Name;
                    }

                    var qtyBase = decimal.Round(x.Qty * factor, 3, MidpointRounding.AwayFromZero);
                    var lineTotal = (x.Qty * x.UnitPrice) - x.Discount;
                    return (Ok: true, Item: x, LineTotal: lineTotal, QtyBase: qtyBase, UnitFactor: factor, UnitName: unitName);
                }).ToArray();

                if (lines.Any(x => !x.Ok))
                {
                    return (StatusCodes.Status400BadRequest, (object)new { error = "INVALID_UNIT" });
                }

                if (lines.Any(x => x.Item.Qty <= 0 || x.Item.UnitPrice < 0 || x.Item.Discount < 0 || x.LineTotal < 0 || x.QtyBase <= 0))
                {
                    return (StatusCodes.Status400BadRequest, (object)new { error = "INVALID_LINE" });
                }

                var allowNegative = configuration.GetValue("Inventory:AllowNegativeStock", false);
                if (!allowNegative)
                {
                    var requiredByProduct = lines
                        .GroupBy(x => x.Item.ProductId)
                        .ToDictionary(g => g.Key, g => g.Sum(x => x.QtyBase == 0 ? x.Item.Qty : x.QtyBase));

                    var productIdsForStock = requiredByProduct.Keys.ToArray();
                    var balances = await db.StockBalances
                        .AsNoTracking()
                        .Where(x => x.BranchId == request.BranchId && productIdsForStock.Contains(x.ProductId))
                        .Select(x => new { x.ProductId, x.Qty })
                        .ToListAsync(innerCt);

                    var balanceByProduct = balances.ToDictionary(x => x.ProductId, x => x.Qty);
                    var insufficient = requiredByProduct
                        .Select(kvp =>
                        {
                            balanceByProduct.TryGetValue(kvp.Key, out var available);
                            return new { ProductId = kvp.Key, Required = kvp.Value, Available = available };
                        })
                        .Where(x => x.Available < x.Required)
                        .ToList();

                    if (insufficient.Count != 0)
                    {
                        return (StatusCodes.Status400BadRequest, (object)new { error = "INSUFFICIENT_STOCK", items = insufficient });
                    }
                }

                var total = lines.Sum(x => x.LineTotal);
                var paymentsTotal = request.Payments.Sum(x => x.Amount);

                if (total <= 0 || paymentsTotal <= 0 || paymentsTotal < total)
                {
                    return (StatusCodes.Status400BadRequest, (object)new { error = "INVALID_PAYMENT_TOTAL" });
                }

                if (request.Payments.Any(x => string.Equals(x.Method, "OnAccount", StringComparison.OrdinalIgnoreCase)) && customerId is null)
                {
                    return (StatusCodes.Status400BadRequest, (object)new { error = "CUSTOMER_REQUIRED_FOR_ONACCOUNT" });
                }

                var saleId = operationId;

                await using var businessTx = await db.Database.BeginTransactionAsync(innerCt);

                var saleSeq = await NextCounterAsync(db, tenantId, "sale_no", innerCt);
                var saleNo = $"S-{now:yyyyMMdd}-{saleSeq:000000}";

                var sale = new Sale
                {
                    Id = saleId,
                    TenantId = tenantId,
                    BranchId = request.BranchId,
                    DeviceId = deviceId,
                    CustomerId = customerId,
                    CustomerName = customerName,
                    CustomerTaxRegistrationNo = customerTaxRegistrationNo,
                    CustomerAddress = customerAddress,
                    Number = saleNo,
                    At = request.At ?? now,
                    Total = total,
                    TaxTotal = 0,
                    Status = SaleStatus.Completed
                };
                db.Sales.Add(sale);

                var productCostById = products.ToDictionary(x => x.Id, x => x.Cost);
                var taxRateIds = products.Where(x => x.TaxRateId != null).Select(x => x.TaxRateId!.Value).Distinct().ToArray();
                var taxPercentByRateId = taxRateIds.Length == 0
                    ? new Dictionary<Guid, decimal>()
                    : await db.TaxRates
                        .Where(x => taxRateIds.Contains(x.Id))
                        .Select(x => new { x.Id, x.Percent })
                        .ToDictionaryAsync(x => x.Id, x => x.Percent, innerCt);

                var taxRateIdByProductId = products.ToDictionary(x => x.Id, x => x.TaxRateId);

                var saleItems = lines.Select(x =>
                {
                    var taxRateId = taxRateIdByProductId[x.Item.ProductId];
                    var taxPercent = (taxRateId != null && taxPercentByRateId.TryGetValue(taxRateId.Value, out var p))
                        ? p
                        : 0m;
                    var taxAmount = ComputeIncludedTaxAmount(x.LineTotal, taxPercent);

                    return new SaleItem
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        SaleId = saleId,
                        ProductId = x.Item.ProductId,
                        Qty = x.Item.Qty,
                        QtyBase = x.QtyBase,
                        ProductUnitId = x.Item.ProductUnitId,
                        UnitName = x.UnitName,
                        UnitFactor = x.UnitFactor,
                        UnitPrice = x.Item.UnitPrice,
                        Discount = x.Item.Discount,
                        LineTotal = x.LineTotal,
                        UnitCost = decimal.Round(productCostById[x.Item.ProductId] * x.UnitFactor, 3, MidpointRounding.AwayFromZero),
                        TaxPercent = taxPercent,
                        TaxAmount = taxAmount
                    };
                }).ToList();
                db.SaleItems.AddRange(saleItems);

                sale.TaxTotal = saleItems.Sum(x => x.TaxAmount);

                var payments = request.Payments
                    .Where(x => x.Amount > 0)
                    .Select(x => new Payment
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        SaleId = saleId,
                        Method = x.Method.Trim(),
                        Amount = x.Amount,
                        PaidAt = now,
                        ReferenceNo = string.IsNullOrWhiteSpace(x.ReferenceNo) ? null : x.ReferenceNo.Trim(),
                        Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim()
                    })
                    .ToList();
                db.Payments.AddRange(payments);

                var paymentsTotalApplied = payments.Sum(x => x.Amount);
                var change = decimal.Round(paymentsTotalApplied - total, 3, MidpointRounding.AwayFromZero);
                if (change < 0)
                {
                    return (StatusCodes.Status400BadRequest, (object)new { error = "INVALID_PAYMENT_TOTAL" });
                }

                if (change > 0)
                {
                    var cashReceived = payments.Where(x => string.Equals(x.Method, "Cash", StringComparison.OrdinalIgnoreCase)).Sum(x => x.Amount);
                    if (cashReceived < change)
                    {
                        return (StatusCodes.Status400BadRequest, (object)new { error = "CHANGE_REQUIRES_CASH" });
                    }
                }

                var ledgerEntries = saleItems.Select(x => new StockLedger
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    BranchId = request.BranchId,
                    ProductId = x.ProductId,
                    QtyDelta = -(x.QtyBase == 0 ? x.Qty : x.QtyBase),
                    Reason = StockLedgerReason.Sale,
                    RefType = "Sale",
                    RefId = saleId,
                    At = now
                }).ToList();
                db.StockLedgers.AddRange(ledgerEntries);

                await ApplyStockBalanceDeltasAsync(db, tenantId, request.BranchId, ledgerEntries, innerCt);

                var cashAmount = payments
                    .Where(x => string.Equals(x.Method, "Cash", StringComparison.OrdinalIgnoreCase))
                    .Sum(x => x.Amount);

                var netCash = cashAmount - change;
                if (netCash != 0)
                {
                    db.CashTxns.Add(new CashTxn
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        BranchId = request.BranchId,
                        Type = CashTxnType.SaleIn,
                        Amount = netCash,
                        Note = request.Note,
                        RefType = "Sale",
                        RefId = saleId,
                        At = now
                    });
                }

                var taxLedgerLines = saleItems
                    .GroupBy(x =>
                    {
                        var rateId = taxRateIdByProductId[x.ProductId];
                        return (TaxRateId: rateId, TaxPercent: x.TaxPercent);
                    })
                    .Select(g => new TaxEngine.Line(g.Key.TaxRateId, g.Key.TaxPercent, decimal.Round(g.Sum(x => x.TaxAmount), 4, MidpointRounding.AwayFromZero)))
                    .ToList();
                TaxEngine.AddTaxLedgerEntries(db, tenantId, request.BranchId, now, TaxLedgerType.Output, "Sale", saleId, taxLedgerLines);

                var tenant = await db.Tenants.AsNoTracking().SingleAsync(x => x.Id == tenantId, innerCt);
                var qrPayload = System.Text.Json.JsonSerializer.Serialize(new
                {
                    companyName = tenant.Name,
                    taxNumber = tenant.TaxRegistrationNo,
                    at = now,
                    total,
                    vat = sale.TaxTotal
                }, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
                sale.QrCodeBase64 = QrCodeEngine.EncodeSvgBase64(qrPayload);

                var netSales = decimal.Round(total - sale.TaxTotal, 4, MidpointRounding.AwayFromZero);
                var vatOut = decimal.Round(sale.TaxTotal, 4, MidpointRounding.AwayFromZero);

                var paymentLines = payments
                    .GroupBy(x => x.Method, StringComparer.OrdinalIgnoreCase)
                    .Select(g => new { Method = g.Key, Amount = decimal.Round(g.Sum(x => x.Amount), 4, MidpointRounding.AwayFromZero) })
                    .Where(x => x.Amount != 0)
                    .ToList();

                static string? MapPaymentMethodToAccountCode(string method) =>
                    method.Trim().ToLowerInvariant() switch
                    {
                        "cash" => AccountingEngine.Codes.Cash,
                        "visa" => AccountingEngine.Codes.VisaClearing,
                        "bank" => AccountingEngine.Codes.Bank,
                        "transfer" => AccountingEngine.Codes.Bank,
                        "onaccount" => AccountingEngine.Codes.AccountsReceivable,
                        _ => null
                    };

                var journalLines = new List<AccountingEngine.Line>();
                foreach (var pl in paymentLines)
                {
                    var code = MapPaymentMethodToAccountCode(pl.Method);
                    if (code is null)
                    {
                        return (StatusCodes.Status400BadRequest, (object)new { error = "UNKNOWN_PAYMENT_METHOD", method = pl.Method });
                    }

                    journalLines.Add(new AccountingEngine.Line(code, Debit: pl.Amount, Credit: 0m, CustomerId: customerId));
                }

                if (change > 0)
                {
                    journalLines.Add(new AccountingEngine.Line(AccountingEngine.Codes.Cash, Debit: 0m, Credit: decimal.Round(change, 4, MidpointRounding.AwayFromZero), CustomerId: customerId, Note: "Change"));
                }

                journalLines.Add(new AccountingEngine.Line(AccountingEngine.Codes.SalesRevenue, Debit: 0m, Credit: netSales));
                if (vatOut != 0)
                {
                    journalLines.Add(new AccountingEngine.Line(AccountingEngine.Codes.OutputVat, Debit: 0m, Credit: vatOut));
                }

                var (journalOk, journalError) = await AccountingEngine.TryAddJournalEntryAsync(
                    db,
                    tenantId,
                    request.BranchId,
                    now,
                    "Sale",
                    saleId,
                    $"Sale {saleNo}",
                    journalLines,
                    innerCt);

                if (!journalOk)
                {
                    return (StatusCodes.Status400BadRequest, (object)new { error = journalError });
                }

                var receipt = new
                {
                    type = "sale",
                    saleId,
                    saleNo,
                    customerId,
                    customerName,
                    at = now,
                    items = saleItems.Select(x => new
                    {
                        x.ProductId,
                        x.Qty,
                        x.UnitPrice,
                        x.Discount,
                        x.LineTotal
                    }),
                    payments = payments.Select(x => new { x.Method, x.Amount }),
                    total,
                    taxTotal = sale.TaxTotal,
                    change
                };

                db.PrintJobs.Add(new PrintJob
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    BranchId = request.BranchId,
                    DeviceId = deviceId,
                    RefType = "Sale",
                    RefId = saleId,
                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(receipt, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)),
                    Status = PrintJobStatus.Pending,
                    Attempts = 0,
                    NextRetryAt = now,
                    CreatedAt = now,
                    UpdatedAt = now
                });

                await db.SaveChangesAsync(innerCt);
                await businessTx.CommitAsync(innerCt);

                return (StatusCodes.Status201Created, (object)new CreateSaleResponse(saleId, saleNo, total));
            },
            ttl,
            ct);
    }

    [HttpPost("returns")]
    [RequirePermission(PermissionKeys.ReturnsCreate)]
    public async Task<IActionResult> CreateReturn([FromBody] CreateReturnRequest request, CancellationToken ct)
    {
        var tenantId = db.TenantId;
        if (tenantId == Guid.Empty)
        {
            return BadRequest(new { error = "TENANT_REQUIRED", header = "X-Tenant-Id" });
        }

        var deviceClaim = User.FindFirst("device_id")?.Value;
        if (!Guid.TryParse(deviceClaim, out var deviceId) || deviceId == Guid.Empty)
        {
            if (!Request.Headers.TryGetValue("X-Device-Id", out var rawDeviceId) ||
                !Guid.TryParse(rawDeviceId.ToString(), out deviceId) ||
                deviceId == Guid.Empty)
            {
                return BadRequest(new { error = "DEVICE_REQUIRED", header = "X-Device-Id" });
            }
        }

        if (!Request.Headers.TryGetValue("Idempotency-Key", out var rawKey) ||
            string.IsNullOrWhiteSpace(rawKey.ToString()))
        {
            return BadRequest(new { error = "IDEMPOTENCY_KEY_REQUIRED", header = "Idempotency-Key" });
        }

        var key = rawKey.ToString().Trim();
        var endpoint = "POST:/pos/returns";
        var requestHash = IdempotencyService.HashRequest(request);
        var ttlMinutes = configuration.GetValue("Idempotency:PosTtlMinutes", 3);
        var ttl = TimeSpan.FromMinutes(Math.Clamp(ttlMinutes, 1, 30));

        return await idempotencyService.ExecuteAsync(
            db,
            tenantId,
            deviceId,
            endpoint,
            key,
            requestHash,
            async (operationId, innerCt) =>
            {
                var now = DateTimeOffset.UtcNow;

                var existingReturn = await db.Returns
                    .AsNoTracking()
                    .SingleOrDefaultAsync(x => x.Id == operationId, innerCt);
                if (existingReturn is not null)
                {
                    return (StatusCodes.Status201Created, (object)new CreateReturnResponse(existingReturn.Id, existingReturn.Number, existingReturn.Total));
                }

                if (request.BranchId == Guid.Empty || request.OrigSaleId == Guid.Empty)
                {
                    return (StatusCodes.Status400BadRequest, (object)new { error = "INVALID_REQUEST" });
                }

                if (request.Items.Count == 0)
                {
                    return (StatusCodes.Status400BadRequest, (object)new { error = "ITEMS_REQUIRED" });
                }

                var sale = await db.Sales.SingleOrDefaultAsync(x => x.Id == request.OrigSaleId, innerCt);
                if (sale is null)
                {
                    return (StatusCodes.Status404NotFound, (object)new { error = "SALE_NOT_FOUND" });
                }

                var soldItems = await db.SaleItems
                    .Where(x => x.SaleId == request.OrigSaleId)
                    .ToListAsync(innerCt);

                var alreadyReturned = await db.Returns
                    .Where(x => x.OrigSaleId == request.OrigSaleId)
                    .Select(x => x.Id)
                    .ToListAsync(innerCt);

                var alreadyReturnedItems = alreadyReturned.Count == 0
                    ? new List<ReturnItem>()
                    : await db.ReturnItems.Where(x => alreadyReturned.Contains(x.ReturnId)).ToListAsync(innerCt);

                var soldQtyByProduct = soldItems
                    .GroupBy(x => x.ProductId)
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.QtyBase == 0 ? x.Qty : x.QtyBase));

                var returnedQtyByProduct = alreadyReturnedItems
                    .GroupBy(x => x.ProductId)
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.QtyBase == 0 ? x.Qty : x.QtyBase));

                var taxPercentByProduct = soldItems
                    .GroupBy(x => x.ProductId)
                    .ToDictionary(g => g.Key, g => g.Select(x => x.TaxPercent).FirstOrDefault());

                var soldUnitByProduct = soldItems
                    .GroupBy(x => x.ProductId)
                    .ToDictionary(g => g.Key, g => g.First());

                var unitIdsForReturn = request.Items.Where(x => x.ProductUnitId != null).Select(x => x.ProductUnitId!.Value).Distinct().ToArray();
                var unitsForReturn = unitIdsForReturn.Length == 0
                    ? new List<ProductUnit>()
                    : await db.ProductUnits.Where(x => unitIdsForReturn.Contains(x.Id)).ToListAsync(innerCt);

                if (unitsForReturn.Count != unitIdsForReturn.Length)
                {
                    return (StatusCodes.Status400BadRequest, (object)new { error = "INVALID_UNIT" });
                }

                var unitByIdForReturn = unitsForReturn.ToDictionary(x => x.Id, x => x);

                foreach (var item in request.Items)
                {
                    if (item.Qty <= 0)
                    {
                        return (StatusCodes.Status400BadRequest, (object)new { error = "INVALID_LINE" });
                    }

                    if (!soldQtyByProduct.TryGetValue(item.ProductId, out var soldQty))
                    {
                        return (StatusCodes.Status400BadRequest, (object)new { error = "INVALID_PRODUCT" });
                    }

                    returnedQtyByProduct.TryGetValue(item.ProductId, out var returnedQty);

                    if (!soldUnitByProduct.TryGetValue(item.ProductId, out var soldUnit))
                    {
                        return (StatusCodes.Status400BadRequest, (object)new { error = "INVALID_PRODUCT" });
                    }

                    var factor = soldUnit.UnitFactor == 0 ? 1m : soldUnit.UnitFactor;
                    if (item.ProductUnitId != null)
                    {
                        var unit = unitByIdForReturn[item.ProductUnitId.Value];
                        if (!unit.IsActive || unit.ProductId != item.ProductId || unit.Factor <= 0)
                        {
                            return (StatusCodes.Status400BadRequest, (object)new { error = "INVALID_UNIT" });
                        }

                        factor = unit.Factor;
                    }

                    var qtyBase = decimal.Round(item.Qty * factor, 3, MidpointRounding.AwayFromZero);
                    if (qtyBase <= 0 || returnedQty + qtyBase > soldQty)
                    {
                        return (StatusCodes.Status409Conflict, (object)new { error = "RETURN_EXCEEDS_SOLD_QTY" });
                    }
                }

                var lines = request.Items.Select(x =>
                {
                    var lineTotal = (x.Qty * x.UnitPrice) - x.Discount;
                    taxPercentByProduct.TryGetValue(x.ProductId, out var taxPercent);

                    soldUnitByProduct.TryGetValue(x.ProductId, out var soldUnit);
                    var factor = soldUnit?.UnitFactor == 0 ? 1m : (soldUnit?.UnitFactor ?? 1m);
                    string? unitName = soldUnit?.UnitName;
                    if (x.ProductUnitId != null)
                    {
                        var unit = unitByIdForReturn[x.ProductUnitId.Value];
                        factor = unit.Factor;
                        unitName = unit.Name;
                    }

                    var qtyBase = decimal.Round(x.Qty * factor, 3, MidpointRounding.AwayFromZero);
                    return (Item: x, LineTotal: lineTotal, TaxPercent: taxPercent, QtyBase: qtyBase, UnitFactor: factor, UnitName: unitName);
                }).ToArray();

                if (lines.Any(x => x.Item.UnitPrice < 0 || x.Item.Discount < 0 || x.LineTotal < 0 || x.QtyBase <= 0))
                {
                    return (StatusCodes.Status400BadRequest, (object)new { error = "INVALID_LINE" });
                }

                var total = lines.Sum(x => x.LineTotal);
                if (total <= 0)
                {
                    return (StatusCodes.Status400BadRequest, (object)new { error = "INVALID_TOTAL" });
                }

                var returnId = operationId;

                await using var businessTx = await db.Database.BeginTransactionAsync(innerCt);

                var returnSeq = await NextCounterAsync(db, tenantId, "return_no", innerCt);
                var returnNo = $"R-{now:yyyyMMdd}-{returnSeq:000000}";

                var ret = new Return
                {
                    Id = returnId,
                    TenantId = tenantId,
                    BranchId = request.BranchId,
                    DeviceId = deviceId,
                    OrigSaleId = request.OrigSaleId,
                    Number = returnNo,
                    At = now,
                    Total = total,
                    RefundMethod = string.IsNullOrWhiteSpace(request.RefundMethod) ? "Cash" : request.RefundMethod.Trim()
                };
                db.Returns.Add(ret);

                var returnItems = lines.Select(x => new ReturnItem
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ReturnId = returnId,
                    ProductId = x.Item.ProductId,
                    Qty = x.Item.Qty,
                    QtyBase = x.QtyBase,
                    ProductUnitId = x.Item.ProductUnitId ?? (soldUnitByProduct.TryGetValue(x.Item.ProductId, out var soldUnit) ? soldUnit.ProductUnitId : null),
                    UnitName = x.UnitName,
                    UnitFactor = x.UnitFactor,
                    UnitPrice = x.Item.UnitPrice,
                    Discount = x.Item.Discount,
                    LineTotal = x.LineTotal,
                    TaxPercent = x.TaxPercent,
                    TaxAmount = ComputeIncludedTaxAmount(x.LineTotal, x.TaxPercent)
                }).ToList();
                db.ReturnItems.AddRange(returnItems);

                var ledgerEntries = returnItems.Select(x => new StockLedger
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    BranchId = request.BranchId,
                    ProductId = x.ProductId,
                    QtyDelta = x.QtyBase == 0 ? x.Qty : x.QtyBase,
                    Reason = StockLedgerReason.Return,
                    RefType = "Return",
                    RefId = returnId,
                    At = now
                }).ToList();
                db.StockLedgers.AddRange(ledgerEntries);

                await ApplyStockBalanceDeltasAsync(db, tenantId, request.BranchId, ledgerEntries, innerCt);

                if (string.Equals(ret.RefundMethod, "Cash", StringComparison.OrdinalIgnoreCase))
                {
                    db.CashTxns.Add(new CashTxn
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        BranchId = request.BranchId,
                        Type = CashTxnType.RefundOut,
                        Amount = -total,
                        Note = request.Note,
                        RefType = "Return",
                        RefId = returnId,
                        At = now
                    });
                }

                var returnTaxTotal = decimal.Round(returnItems.Sum(x => x.TaxAmount), 4, MidpointRounding.AwayFromZero);
                var returnNet = decimal.Round(total - returnTaxTotal, 4, MidpointRounding.AwayFromZero);

                var returnTaxLedgerLines = returnItems
                    .GroupBy(x => x.TaxPercent)
                    .Select(g => new TaxEngine.Line(TaxRateId: null, TaxPercent: g.Key, Amount: -decimal.Round(g.Sum(x => x.TaxAmount), 4, MidpointRounding.AwayFromZero)))
                    .ToList();
                TaxEngine.AddTaxLedgerEntries(db, tenantId, request.BranchId, now, TaxLedgerType.Output, "Return", returnId, returnTaxLedgerLines);

                static string? MapRefundMethodToAccountCode(string method) =>
                    method.Trim().ToLowerInvariant() switch
                    {
                        "cash" => AccountingEngine.Codes.Cash,
                        "visa" => AccountingEngine.Codes.VisaClearing,
                        "bank" => AccountingEngine.Codes.Bank,
                        "transfer" => AccountingEngine.Codes.Bank,
                        "onaccount" => AccountingEngine.Codes.AccountsReceivable,
                        _ => null
                    };

                var refundCode = MapRefundMethodToAccountCode(ret.RefundMethod);
                if (refundCode is null)
                {
                    return (StatusCodes.Status400BadRequest, (object)new { error = "UNKNOWN_REFUND_METHOD", method = ret.RefundMethod });
                }

                var returnJournalLines = new List<AccountingEngine.Line>
                {
                    new(AccountingEngine.Codes.SalesReturns, Debit: returnNet, Credit: 0m),
                    new(AccountingEngine.Codes.OutputVat, Debit: returnTaxTotal, Credit: 0m),
                    new(refundCode, Debit: 0m, Credit: decimal.Round(total, 4, MidpointRounding.AwayFromZero))
                };

                var (returnJournalOk, returnJournalError) = await AccountingEngine.TryAddJournalEntryAsync(
                    db,
                    tenantId,
                    request.BranchId,
                    now,
                    "Return",
                    returnId,
                    $"Return {returnNo}",
                    returnJournalLines,
                    innerCt);

                if (!returnJournalOk)
                {
                    return (StatusCodes.Status400BadRequest, (object)new { error = returnJournalError });
                }

                var receipt = new
                {
                    type = "return",
                    returnId,
                    returnNo,
                    origSaleId = request.OrigSaleId,
                    at = now,
                    items = returnItems.Select(x => new
                    {
                        x.ProductId,
                        x.Qty,
                        x.UnitPrice,
                        x.Discount,
                        x.LineTotal
                    }),
                    total,
                    taxTotal = returnTaxTotal,
                    refundMethod = ret.RefundMethod
                };

                db.PrintJobs.Add(new PrintJob
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    BranchId = request.BranchId,
                    DeviceId = deviceId,
                    RefType = "Return",
                    RefId = returnId,
                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(receipt, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)),
                    Status = PrintJobStatus.Pending,
                    Attempts = 0,
                    NextRetryAt = now,
                    CreatedAt = now,
                    UpdatedAt = now
                });

                await db.SaveChangesAsync(innerCt);
                await businessTx.CommitAsync(innerCt);

                return (StatusCodes.Status201Created, (object)new CreateReturnResponse(returnId, returnNo, total));
            },
            ttl,
            ct);
    }

    private static string? BuildPartyAddress(
        string? country,
        string? governorate,
        string? city,
        string? buildingNo,
        string? floor,
        string? apartment,
        string? streetName,
        string? postalCode,
        string? freeForm)
    {
        var parts = new List<string>(10);

        if (!string.IsNullOrWhiteSpace(country)) parts.Add(country.Trim());
        if (!string.IsNullOrWhiteSpace(governorate)) parts.Add(governorate.Trim());
        if (!string.IsNullOrWhiteSpace(city)) parts.Add(city.Trim());
        if (!string.IsNullOrWhiteSpace(buildingNo)) parts.Add(buildingNo.Trim());
        if (!string.IsNullOrWhiteSpace(floor)) parts.Add(floor.Trim());
        if (!string.IsNullOrWhiteSpace(apartment)) parts.Add(apartment.Trim());
        if (!string.IsNullOrWhiteSpace(streetName)) parts.Add(streetName.Trim());
        if (!string.IsNullOrWhiteSpace(postalCode)) parts.Add(postalCode.Trim());
        if (!string.IsNullOrWhiteSpace(freeForm)) parts.Add(freeForm.Trim());

        return parts.Count == 0 ? null : string.Join(" - ", parts);
    }

    private static decimal ComputeIncludedTaxAmount(decimal lineTotal, decimal percent)
    {
        if (lineTotal <= 0 || percent <= 0)
        {
            return 0m;
        }

        if (percent > 1m)
        {
            percent /= 100m;
        }

        if (percent > 1)
        {
            percent = 1;
        }

        var tax = lineTotal * (percent / (1 + percent));
        return decimal.Round(tax, 3, MidpointRounding.AwayFromZero);
    }

    private static async Task ApplyStockBalanceDeltasAsync(
        AppDbContext db,
        Guid tenantId,
        Guid branchId,
        IReadOnlyList<StockLedger> entries,
        CancellationToken ct)
    {
        var deltas = entries
            .GroupBy(x => x.ProductId)
            .Select(g => (ProductId: g.Key, QtyDelta: g.Sum(x => x.QtyDelta)))
            .ToList();

        await StockBalanceWriter.ApplyDeltasAsync(db, tenantId, branchId, deltas, ct);
    }

    private async Task<long> NextCounterAsync(AppDbContext db, Guid tenantId, string name, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var allocated = await TryAllocateFromExistingCounterAsync(db, tenantId, name, ct);
            if (allocated is not null)
            {
                return allocated.Value;
            }

            db.Counters.Add(new Counter
            {
                TenantId = tenantId,
                Name = name,
                NextValue = 2
            });

            try
            {
                await db.SaveChangesAsync(ct);
                return 1;
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                db.ChangeTracker.Clear();
                continue;
            }
        }

        throw new InvalidOperationException($"Failed to allocate counter value for '{name}'.");
    }

    private static async Task<long?> TryAllocateFromExistingCounterAsync(AppDbContext db, Guid tenantId, string name, CancellationToken ct)
    {
        var provider = db.Database.ProviderName ?? "";
        if (provider.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            var updated = await db.Database.SqlQuery<long>(
                $"""
                 UPDATE [Counters]
                 SET [NextValue] = [NextValue] + 1
                 OUTPUT inserted.[NextValue]
                 WHERE [TenantId] = {tenantId} AND [Name] = {name}
                 """).ToListAsync(ct);

            return updated.Count == 0 ? null : updated[0] - 1;
        }

        var updatedPostgres = await db.Database.SqlQuery<long>(
            $"""
             UPDATE "Counters"
             SET "NextValue" = "NextValue" + 1
             WHERE "TenantId" = {tenantId} AND "Name" = {name}
             RETURNING "NextValue"
             """).ToListAsync(ct);

        return updatedPostgres.Count == 0 ? null : updatedPostgres[0] - 1;
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        if (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return true;
        }

        return ex.InnerException is SqlException { Number: 2601 or 2627 };
    }
}
