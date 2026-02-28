using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniErp.Api.Contracts.Purchases;
using MiniErp.Api.Data;
using MiniErp.Api.Domain;
using MiniErp.Api.Domain.Enums;
using MiniErp.Api.Infrastructure.Inventory;
using MiniErp.Api.Security;

namespace MiniErp.Api.Controllers;

[ApiController]
[Route("purchases")]
[Authorize]
public sealed class PurchasesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionKeys.PurchasesView)]
    public async Task<ActionResult<IReadOnlyList<PurchaseListItem>>> List(
        [FromQuery] Guid branchId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 200,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        [FromQuery] Guid? supplierId = null,
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

        if (supplierId == Guid.Empty)
        {
            supplierId = null;
        }

        if (to is not null && from is not null && to.Value < from.Value)
        {
            return BadRequest(new { error = "INVALID_DATE_RANGE" });
        }

        var q = db.Purchases
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

        if (supplierId is not null)
        {
            q = q.Where(x => x.SupplierId == supplierId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(x => EF.Functions.Like(x.Number, $"%{s}%") || (x.SupplierName != null && x.SupplierName.Contains(s)));
        }

        var items = await q
            .OrderByDescending(x => x.At)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new PurchaseListItem(x.Id, x.Number, x.BranchId, x.At, x.SupplierName, x.Total, x.CashPaid, x.TaxTotal))
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpPost]
    [RequirePermission(PermissionKeys.PurchasesCreate)]
    public async Task<IActionResult> Create([FromBody] CreatePurchaseRequest request, CancellationToken ct)
    {
        var tenantId = db.TenantId;
        if (tenantId == Guid.Empty)
        {
            return BadRequest(new { error = "TENANT_REQUIRED" });
        }

        if (request.BranchId == Guid.Empty)
        {
            return BadRequest(new { error = "BRANCH_REQUIRED" });
        }

        if (request.Items.Count == 0)
        {
            return BadRequest(new { error = "ITEMS_REQUIRED" });
        }

        var userId = GetUserId();
        if (userId == Guid.Empty)
        {
            return BadRequest(new { error = "USER_REQUIRED" });
        }

        var deviceId = GetDeviceId();
        if (deviceId == Guid.Empty)
        {
            return BadRequest(new { error = "DEVICE_REQUIRED" });
        }

        if (request.Items.Any(x => x.ProductId == Guid.Empty || x.Qty <= 0 || x.UnitCost < 0))
        {
            return BadRequest(new { error = "INVALID_LINE" });
        }

        Guid? supplierId = null;
        string? supplierName = null;
        if (request.SupplierId is not null && request.SupplierId.Value != Guid.Empty)
        {
            var supplier = await db.Suppliers.SingleOrDefaultAsync(x => x.Id == request.SupplierId.Value && x.IsActive, ct);
            if (supplier is null)
            {
                return BadRequest(new { error = "INVALID_SUPPLIER" });
            }

            supplierId = supplier.Id;
            supplierName = supplier.Name;
        }
        else if (!string.IsNullOrWhiteSpace(request.SupplierName))
        {
            supplierName = request.SupplierName.Trim();
        }

        var productIds = request.Items.Select(x => x.ProductId).Distinct().ToArray();
        var products = await db.Products
            .Where(x => productIds.Contains(x.Id))
            .Select(x => new { x.Id, x.IsActive, x.TaxRateId })
            .ToListAsync(ct);

        if (products.Count != productIds.Length)
        {
            return BadRequest(new { error = "INVALID_PRODUCT" });
        }

        if (products.Any(x => !x.IsActive))
        {
            return BadRequest(new { error = "INACTIVE_PRODUCT" });
        }

        var unitIds = request.Items.Where(x => x.ProductUnitId != null).Select(x => x.ProductUnitId!.Value).Distinct().ToArray();
        var units = unitIds.Length == 0
            ? new List<ProductUnit>()
            : await db.ProductUnits.Where(x => unitIds.Contains(x.Id)).ToListAsync(ct);

        if (units.Count != unitIds.Length)
        {
            return BadRequest(new { error = "INVALID_UNIT" });
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

            var lineTotal = x.Qty * x.UnitCost;
            var qtyBase = decimal.Round(x.Qty * factor, 3, MidpointRounding.AwayFromZero);
            return (Ok: true, Item: x, LineTotal: lineTotal, QtyBase: qtyBase, UnitFactor: factor, UnitName: unitName);
        }).ToArray();

        if (lines.Any(x => !x.Ok))
        {
            return BadRequest(new { error = "INVALID_UNIT" });
        }

        if (lines.Any(x => x.LineTotal < 0 || x.QtyBase <= 0))
        {
            return BadRequest(new { error = "INVALID_LINE_TOTAL" });
        }

        var taxRateIds = products.Where(x => x.TaxRateId != null).Select(x => x.TaxRateId!.Value).Distinct().ToArray();
        var taxPercentByRateId = taxRateIds.Length == 0
            ? new Dictionary<Guid, decimal>()
            : await db.TaxRates
                .Where(x => taxRateIds.Contains(x.Id))
                .Select(x => new { x.Id, x.Percent })
                .ToDictionaryAsync(x => x.Id, x => x.Percent, ct);

        var taxRateIdByProductId = products.ToDictionary(x => x.Id, x => x.TaxRateId);

        var total = lines.Sum(x => x.LineTotal);
        if (total <= 0)
        {
            return BadRequest(new { error = "INVALID_TOTAL" });
        }

        if (request.CashPaid < 0 || request.CashPaid > total)
        {
            return BadRequest(new { error = "INVALID_CASH_PAID" });
        }

        var now = DateTimeOffset.UtcNow;
        var purchaseId = Guid.NewGuid();
        var seq = await NextCounterAsync(db, tenantId, "pur_no", ct);
        var purchaseNo = $"P-{now:yyyyMMdd}-{seq:000000}";

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var purchase = new Purchase
        {
            Id = purchaseId,
            TenantId = tenantId,
            BranchId = request.BranchId,
            DeviceId = deviceId,
            UserId = userId,
            Number = purchaseNo,
            At = now,
            SupplierId = supplierId,
            SupplierName = supplierName,
            Total = total,
            TaxTotal = 0,
            CashPaid = request.CashPaid,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim()
        };
        db.Purchases.Add(purchase);

        var purchaseItems = lines.Select(x =>
        {
            var rateId = taxRateIdByProductId[x.Item.ProductId];
            var taxPercent = (rateId != null && taxPercentByRateId.TryGetValue(rateId.Value, out var p))
                ? p
                : 0m;
            var taxAmount = ComputeIncludedTaxAmount(x.LineTotal, taxPercent);

            return new PurchaseItem
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                PurchaseId = purchaseId,
                ProductId = x.Item.ProductId,
                Qty = x.Item.Qty,
                QtyBase = x.QtyBase,
                ProductUnitId = x.Item.ProductUnitId,
                UnitName = x.UnitName,
                UnitFactor = x.UnitFactor,
                UnitCost = x.Item.UnitCost,
                LineTotal = x.LineTotal,
                TaxPercent = taxPercent,
                TaxAmount = taxAmount
            };
        }).ToList();
        db.PurchaseItems.AddRange(purchaseItems);
        purchase.TaxTotal = purchaseItems.Sum(x => x.TaxAmount);

        var ledger = purchaseItems.Select(x => new StockLedger
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BranchId = request.BranchId,
            ProductId = x.ProductId,
            QtyDelta = x.QtyBase == 0 ? x.Qty : x.QtyBase,
            Reason = StockLedgerReason.Purchase,
            RefType = "Purchase",
            RefId = purchaseId,
            At = now
        }).ToList();
        db.StockLedgers.AddRange(ledger);

        await ApplyStockBalanceDeltasAsync(db, tenantId, request.BranchId, ledger, ct);

        if (request.CashPaid != 0)
        {
            db.CashTxns.Add(new CashTxn
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                BranchId = request.BranchId,
                Type = CashTxnType.ExpenseOut,
                Amount = request.CashPaid,
                Note = "Purchase cash payment",
                RefType = "Purchase",
                RefId = purchaseId,
                At = now
            });
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Created("", new CreatePurchaseResponse(purchaseId, purchaseNo, total));
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

    private Guid GetUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(sub, out var userId) ? userId : Guid.Empty;
    }

    private Guid GetDeviceId()
    {
        var claim = User.FindFirstValue("device_id");
        if (Guid.TryParse(claim, out var deviceId) && deviceId != Guid.Empty)
        {
            return deviceId;
        }

        if (!Request.Headers.TryGetValue("X-Device-Id", out var raw) || !Guid.TryParse(raw.ToString(), out deviceId))
        {
            return Guid.Empty;
        }

        return deviceId;
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

    private static async Task<long> NextCounterAsync(AppDbContext db, Guid tenantId, string name, CancellationToken ct)
    {
        var provider = db.Database.ProviderName ?? "";
        if (provider.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            for (var attempt = 0; attempt < 5; attempt++)
            {
                var updated = await db.Database.SqlQuery<long>(
                    $"""
                     UPDATE [Counters]
                     SET [NextValue] = [NextValue] + 1
                     OUTPUT inserted.[NextValue]
                     WHERE [TenantId] = {tenantId} AND [Name] = {name}
                     """).ToListAsync(ct);

                if (updated.Count != 0)
                {
                    return updated[0] - 1;
                }

                db.Counters.Add(new Counter { TenantId = tenantId, Name = name, NextValue = 2 });
                try
                {
                    await db.SaveChangesAsync(ct);
                    return 1;
                }
                catch (DbUpdateException)
                {
                    db.ChangeTracker.Clear();
                }
            }

            throw new InvalidOperationException($"Failed to allocate counter value for '{name}'.");
        }

        var updatedPostgres = await db.Database.SqlQuery<long>(
            $"""
             UPDATE "Counters"
             SET "NextValue" = "NextValue" + 1
             WHERE "TenantId" = {tenantId} AND "Name" = {name}
             RETURNING "NextValue"
             """).ToListAsync(ct);

        if (updatedPostgres.Count == 0)
        {
            db.Counters.Add(new Counter { TenantId = tenantId, Name = name, NextValue = 2 });
            await db.SaveChangesAsync(ct);
            return 1;
        }

        return updatedPostgres[0] - 1;
    }
}
