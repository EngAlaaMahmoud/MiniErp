using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MiniErp.Api.Contracts.Pos;
using MiniErp.Api.Data;
using MiniErp.Api.Domain;
using MiniErp.Api.Domain.Enums;
using MiniErp.Api.Services;
using Npgsql;

namespace MiniErp.Api.Controllers;

[ApiController]
[Route("pos")]
public sealed class PosController(AppDbContext db, IdempotencyService idempotencyService) : ControllerBase
{
    [HttpPost("sales")]
    public async Task<IActionResult> CreateSale([FromBody] CreateSaleRequest request, CancellationToken ct)
    {
        var tenantId = db.TenantId;
        if (tenantId == Guid.Empty)
        {
            return BadRequest(new { error = "TENANT_REQUIRED", header = "X-Tenant-Id" });
        }

        if (!Request.Headers.TryGetValue("X-Device-Id", out var rawDeviceId) ||
            !Guid.TryParse(rawDeviceId.ToString(), out var deviceId) ||
            deviceId == Guid.Empty)
        {
            return BadRequest(new { error = "DEVICE_REQUIRED", header = "X-Device-Id" });
        }

        if (!Request.Headers.TryGetValue("Idempotency-Key", out var rawKey) ||
            string.IsNullOrWhiteSpace(rawKey.ToString()))
        {
            return BadRequest(new { error = "IDEMPOTENCY_KEY_REQUIRED", header = "Idempotency-Key" });
        }

        var key = rawKey.ToString().Trim();
        var endpoint = "POST:/pos/sales";
        var requestHash = IdempotencyService.HashRequest(request);

        return await idempotencyService.ExecuteAsync(
            db,
            tenantId,
            deviceId,
            endpoint,
            key,
            requestHash,
            async innerCt =>
            {
                var now = DateTimeOffset.UtcNow;

                if (request.BranchId == Guid.Empty)
                {
                    return (StatusCodes.Status400BadRequest, (object)new { error = "BRANCH_REQUIRED" });
                }

                if (request.Items.Count == 0)
                {
                    return (StatusCodes.Status400BadRequest, (object)new { error = "ITEMS_REQUIRED" });
                }

                var productIds = request.Items.Select(x => x.ProductId).Distinct().ToArray();
                var products = await db.Products
                    .Where(x => productIds.Contains(x.Id))
                    .Select(x => new { x.Id, x.Cost, x.IsActive })
                    .ToListAsync(innerCt);

                if (products.Count != productIds.Length)
                {
                    return (StatusCodes.Status400BadRequest, (object)new { error = "INVALID_PRODUCT" });
                }

                if (products.Any(x => !x.IsActive))
                {
                    return (StatusCodes.Status400BadRequest, (object)new { error = "INACTIVE_PRODUCT" });
                }

                var lines = request.Items.Select(x =>
                {
                    var lineTotal = (x.Qty * x.UnitPrice) - x.Discount;
                    return (Item: x, LineTotal: lineTotal);
                }).ToArray();

                if (lines.Any(x => x.Item.Qty <= 0 || x.Item.UnitPrice < 0 || x.Item.Discount < 0 || x.LineTotal < 0))
                {
                    return (StatusCodes.Status400BadRequest, (object)new { error = "INVALID_LINE" });
                }

                var total = lines.Sum(x => x.LineTotal);
                var paymentsTotal = request.Payments.Sum(x => x.Amount);

                if (total <= 0 || paymentsTotal <= 0 || paymentsTotal < total)
                {
                    return (StatusCodes.Status400BadRequest, (object)new { error = "INVALID_PAYMENT_TOTAL" });
                }

                var saleId = Guid.NewGuid();
                var saleSeq = await NextCounterAsync(db, tenantId, "sale_no", innerCt);
                var saleNo = $"S-{now:yyyyMMdd}-{saleSeq:000000}";

                var sale = new Sale
                {
                    Id = saleId,
                    TenantId = tenantId,
                    BranchId = request.BranchId,
                    DeviceId = deviceId,
                    Number = saleNo,
                    At = now,
                    Total = total,
                    Status = SaleStatus.Completed
                };
                db.Sales.Add(sale);

                var productCostById = products.ToDictionary(x => x.Id, x => x.Cost);
                var saleItems = lines.Select(x => new SaleItem
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    SaleId = saleId,
                    ProductId = x.Item.ProductId,
                    Qty = x.Item.Qty,
                    UnitPrice = x.Item.UnitPrice,
                    Discount = x.Item.Discount,
                    LineTotal = x.LineTotal,
                    UnitCost = productCostById[x.Item.ProductId]
                }).ToList();
                db.SaleItems.AddRange(saleItems);

                var payments = request.Payments
                    .Where(x => x.Amount > 0)
                    .Select(x => new Payment
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        SaleId = saleId,
                        Method = x.Method.Trim(),
                        Amount = x.Amount
                    })
                    .ToList();
                db.Payments.AddRange(payments);

                var ledgerEntries = saleItems.Select(x => new StockLedger
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    BranchId = request.BranchId,
                    ProductId = x.ProductId,
                    QtyDelta = -x.Qty,
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

                if (cashAmount != 0)
                {
                    db.CashTxns.Add(new CashTxn
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        BranchId = request.BranchId,
                        Type = CashTxnType.SaleIn,
                        Amount = cashAmount,
                        Note = request.Note,
                        RefType = "Sale",
                        RefId = saleId,
                        At = now
                    });
                }

                var receipt = new
                {
                    type = "sale",
                    saleId,
                    saleNo,
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
                    total
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

                return (StatusCodes.Status201Created, (object)new CreateSaleResponse(saleId, saleNo, total));
            },
            ct);
    }

    [HttpPost("returns")]
    public async Task<IActionResult> CreateReturn([FromBody] CreateReturnRequest request, CancellationToken ct)
    {
        var tenantId = db.TenantId;
        if (tenantId == Guid.Empty)
        {
            return BadRequest(new { error = "TENANT_REQUIRED", header = "X-Tenant-Id" });
        }

        if (!Request.Headers.TryGetValue("X-Device-Id", out var rawDeviceId) ||
            !Guid.TryParse(rawDeviceId.ToString(), out var deviceId) ||
            deviceId == Guid.Empty)
        {
            return BadRequest(new { error = "DEVICE_REQUIRED", header = "X-Device-Id" });
        }

        if (!Request.Headers.TryGetValue("Idempotency-Key", out var rawKey) ||
            string.IsNullOrWhiteSpace(rawKey.ToString()))
        {
            return BadRequest(new { error = "IDEMPOTENCY_KEY_REQUIRED", header = "Idempotency-Key" });
        }

        var key = rawKey.ToString().Trim();
        var endpoint = "POST:/pos/returns";
        var requestHash = IdempotencyService.HashRequest(request);

        return await idempotencyService.ExecuteAsync(
            db,
            tenantId,
            deviceId,
            endpoint,
            key,
            requestHash,
            async innerCt =>
            {
                var now = DateTimeOffset.UtcNow;

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
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.Qty));

                var returnedQtyByProduct = alreadyReturnedItems
                    .GroupBy(x => x.ProductId)
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.Qty));

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
                    if (returnedQty + item.Qty > soldQty)
                    {
                        return (StatusCodes.Status409Conflict, (object)new { error = "RETURN_EXCEEDS_SOLD_QTY" });
                    }
                }

                var lines = request.Items.Select(x =>
                {
                    var lineTotal = (x.Qty * x.UnitPrice) - x.Discount;
                    return (Item: x, LineTotal: lineTotal);
                }).ToArray();

                if (lines.Any(x => x.Item.UnitPrice < 0 || x.Item.Discount < 0 || x.LineTotal < 0))
                {
                    return (StatusCodes.Status400BadRequest, (object)new { error = "INVALID_LINE" });
                }

                var total = lines.Sum(x => x.LineTotal);
                if (total <= 0)
                {
                    return (StatusCodes.Status400BadRequest, (object)new { error = "INVALID_TOTAL" });
                }

                var returnId = Guid.NewGuid();
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
                    UnitPrice = x.Item.UnitPrice,
                    Discount = x.Item.Discount,
                    LineTotal = x.LineTotal
                }).ToList();
                db.ReturnItems.AddRange(returnItems);

                var ledgerEntries = returnItems.Select(x => new StockLedger
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    BranchId = request.BranchId,
                    ProductId = x.ProductId,
                    QtyDelta = x.Qty,
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

                return (StatusCodes.Status201Created, (object)new CreateReturnResponse(returnId, returnNo, total));
            },
            ct);
    }

    private static async Task ApplyStockBalanceDeltasAsync(
        AppDbContext db,
        Guid tenantId,
        Guid branchId,
        IReadOnlyList<StockLedger> entries,
        CancellationToken ct)
    {
        var productIds = entries.Select(x => x.ProductId).Distinct().ToArray();
        var balances = await db.StockBalances
            .Where(x => x.BranchId == branchId && productIds.Contains(x.ProductId))
            .ToListAsync(ct);

        var balanceByProduct = balances.ToDictionary(x => x.ProductId, x => x);

        foreach (var entry in entries)
        {
            if (!balanceByProduct.TryGetValue(entry.ProductId, out var balance))
            {
                balance = new StockBalance
                {
                    TenantId = tenantId,
                    BranchId = branchId,
                    ProductId = entry.ProductId,
                    Qty = 0
                };
                db.StockBalances.Add(balance);
                balanceByProduct[entry.ProductId] = balance;
            }

            balance.Qty += entry.QtyDelta;
        }
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
