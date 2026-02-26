using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniErp.Api.Contracts.Inventory;
using MiniErp.Api.Data;
using MiniErp.Api.Domain;
using MiniErp.Api.Domain.Enums;

namespace MiniErp.Api.Controllers;

[ApiController]
[Route("inventory")]
[Authorize]
public sealed class InventoryController(AppDbContext db) : ControllerBase
{
    [HttpGet("balance")]
    public async Task<IActionResult> GetBalance([FromQuery] Guid branchId, CancellationToken ct)
    {
        if (branchId == Guid.Empty)
        {
            return BadRequest(new { error = "BRANCH_REQUIRED" });
        }

        var items = await db.StockBalances
            .Where(x => x.BranchId == branchId)
            .Join(db.Products, sb => sb.ProductId, p => p.Id, (sb, p) => new StockBalanceItem(sb.ProductId, p.Name, sb.Qty))
            .OrderBy(x => x.ProductName)
            .Take(2000)
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpPost("adjustments")]
    public async Task<IActionResult> CreateAdjustment([FromBody] CreateAdjustmentRequest request, CancellationToken ct)
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

        if (request.Lines.Count == 0)
        {
            return BadRequest(new { error = "LINES_REQUIRED" });
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

        if (request.Lines.Any(x => x.ProductId == Guid.Empty || x.QtyDelta == 0))
        {
            return BadRequest(new { error = "INVALID_LINE" });
        }

        var now = DateTimeOffset.UtcNow;
        var adjustmentId = Guid.NewGuid();
        var seq = await NextCounterAsync(db, tenantId, "adj_no", ct);
        var adjustmentNo = $"A-{now:yyyyMMdd}-{seq:000000}";

        var adjustment = new InventoryAdjustment
        {
            Id = adjustmentId,
            TenantId = tenantId,
            BranchId = request.BranchId,
            DeviceId = deviceId,
            UserId = userId,
            Number = adjustmentNo,
            Note = request.Note,
            At = now
        };
        db.InventoryAdjustments.Add(adjustment);

        var lines = request.Lines.Select(x => new InventoryAdjustmentLine
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AdjustmentId = adjustmentId,
            ProductId = x.ProductId,
            QtyDelta = x.QtyDelta
        }).ToList();
        db.InventoryAdjustmentLines.AddRange(lines);

        var ledger = lines.Select(x => new StockLedger
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BranchId = request.BranchId,
            ProductId = x.ProductId,
            QtyDelta = x.QtyDelta,
            Reason = StockLedgerReason.Adjustment,
            RefType = "Adjustment",
            RefId = adjustmentId,
            At = now
        }).ToList();
        db.StockLedgers.AddRange(ledger);

        await ApplyStockBalanceDeltasAsync(db, tenantId, request.BranchId, ledger, ct);
        await db.SaveChangesAsync(ct);

        return Created("", new CreateAdjustmentResponse(adjustmentId, adjustmentNo));
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

