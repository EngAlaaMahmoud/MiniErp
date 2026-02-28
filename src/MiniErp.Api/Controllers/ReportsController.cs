using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniErp.Api.Contracts.Reports;
using MiniErp.Api.Data;
using MiniErp.Api.Domain.Enums;
using MiniErp.Api.Security;

namespace MiniErp.Api.Controllers;

[ApiController]
[Route("reports")]
[Authorize]
public sealed class ReportsController(AppDbContext db) : ControllerBase
{
    [HttpGet("lookups/customers")]
    [RequirePermission(PermissionKeys.ReportsView)]
    public async Task<ActionResult<IReadOnlyList<NamedIdItem>>> CustomersLookup([FromQuery] string? search, CancellationToken ct)
    {
        var q = db.Customers.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(x => x.Name.Contains(s) || (x.Phone != null && x.Phone.Contains(s)));
        }

        var items = await q
            .OrderBy(x => x.Name)
            .Take(500)
            .Select(x => new NamedIdItem(x.Id, x.Name))
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpGet("lookups/suppliers")]
    [RequirePermission(PermissionKeys.ReportsView)]
    public async Task<ActionResult<IReadOnlyList<NamedIdItem>>> SuppliersLookup([FromQuery] string? search, CancellationToken ct)
    {
        var q = db.Suppliers.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(x => x.Name.Contains(s) || (x.Phone != null && x.Phone.Contains(s)));
        }

        var items = await q
            .OrderBy(x => x.Name)
            .Take(500)
            .Select(x => new NamedIdItem(x.Id, x.Name))
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpGet("lookups/products")]
    [RequirePermission(PermissionKeys.ReportsView)]
    public async Task<ActionResult<IReadOnlyList<NamedIdItem>>> ProductsLookup([FromQuery] string? search, CancellationToken ct)
    {
        var q = db.Products.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(x => x.Name.Contains(s) || (x.Sku != null && x.Sku.Contains(s)));
        }

        var items = await q
            .OrderBy(x => x.Name)
            .Take(800)
            .Select(x => new NamedIdItem(x.Id, x.Name))
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpGet("sales")]
    [RequirePermission(PermissionKeys.ReportsView)]
    public async Task<ActionResult<SalesReportResponse>> Sales(
        [FromQuery] Guid branchId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] Guid? customerId,
        [FromQuery] string? saleNo,
        CancellationToken ct)
    {
        if (branchId == Guid.Empty)
        {
            return BadRequest(new { error = "BRANCH_REQUIRED" });
        }

        if (to < from)
        {
            return BadRequest(new { error = "INVALID_DATE_RANGE" });
        }

        var fromAt = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var toAt = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        var q = db.Sales
            .AsNoTracking()
            .Where(x => x.BranchId == branchId && x.At >= fromAt && x.At < toAt);

        if (customerId == Guid.Empty)
        {
            customerId = null;
        }

        if (customerId is not null)
        {
            q = q.Where(x => x.CustomerId == customerId.Value);
        }

        if (!string.IsNullOrWhiteSpace(saleNo))
        {
            var s = saleNo.Trim();
            q = q.Where(x => EF.Functions.Like(x.Number, $"%{s}%"));
        }

        var agg = await q
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Count = g.Count(),
                Total = g.Sum(x => x.Total),
                TaxTotal = g.Sum(x => x.TaxTotal)
            })
            .SingleOrDefaultAsync(ct);

        var rows = await q
            .OrderByDescending(x => x.At)
            .Take(1000)
            .Select(x => new SalesReportRow(x.Id, x.Number, x.At, x.CustomerName, x.Total, x.TaxTotal))
            .ToListAsync(ct);

        var summary = agg is null
            ? new SalesReportSummary(0, 0, 0)
            : new SalesReportSummary(agg.Count, agg.Total, agg.TaxTotal);

        return Ok(new SalesReportResponse(from, to, branchId, summary, rows));
    }

    [HttpGet("purchases")]
    [RequirePermission(PermissionKeys.ReportsView)]
    public async Task<ActionResult<PurchasesReportResponse>> Purchases(
        [FromQuery] Guid branchId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] Guid? supplierId,
        [FromQuery] string? purchaseNo,
        CancellationToken ct)
    {
        if (branchId == Guid.Empty)
        {
            return BadRequest(new { error = "BRANCH_REQUIRED" });
        }

        if (to < from)
        {
            return BadRequest(new { error = "INVALID_DATE_RANGE" });
        }

        var fromAt = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var toAt = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        var q = db.Purchases
            .AsNoTracking()
            .Where(x => x.BranchId == branchId && x.At >= fromAt && x.At < toAt);

        if (supplierId == Guid.Empty)
        {
            supplierId = null;
        }

        if (supplierId is not null)
        {
            q = q.Where(x => x.SupplierId == supplierId.Value);
        }

        if (!string.IsNullOrWhiteSpace(purchaseNo))
        {
            var s = purchaseNo.Trim();
            q = q.Where(x => EF.Functions.Like(x.Number, $"%{s}%"));
        }

        var agg = await q
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Count = g.Count(),
                Total = g.Sum(x => x.Total),
                TaxTotal = g.Sum(x => x.TaxTotal),
                CashPaidTotal = g.Sum(x => x.CashPaid)
            })
            .SingleOrDefaultAsync(ct);

        var rows = await q
            .OrderByDescending(x => x.At)
            .Take(1000)
            .Select(x => new PurchasesReportRow(x.Id, x.Number, x.At, x.SupplierName, x.Total, x.TaxTotal, x.CashPaid))
            .ToListAsync(ct);

        var summary = agg is null
            ? new PurchasesReportSummary(0, 0, 0, 0)
            : new PurchasesReportSummary(agg.Count, agg.Total, agg.TaxTotal, agg.CashPaidTotal);

        return Ok(new PurchasesReportResponse(from, to, branchId, summary, rows));
    }

    [HttpGet("cash")]
    [RequirePermission(PermissionKeys.ReportsView)]
    public async Task<ActionResult<CashReportResponse>> Cash(
        [FromQuery] Guid branchId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] CashTxnType? type,
        CancellationToken ct)
    {
        if (branchId == Guid.Empty)
        {
            return BadRequest(new { error = "BRANCH_REQUIRED" });
        }

        if (to < from)
        {
            return BadRequest(new { error = "INVALID_DATE_RANGE" });
        }

        var fromAt = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var toAt = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        var q = db.CashTxns
            .AsNoTracking()
            .Where(x => x.BranchId == branchId && x.At >= fromAt && x.At < toAt);

        if (type is not null)
        {
            q = q.Where(x => x.Type == type.Value);
        }

        var agg = await q
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Net = g.Sum(x => x.Amount),
                In = g.Where(x => x.Amount > 0).Sum(x => x.Amount),
                Out = g.Where(x => x.Amount < 0).Sum(x => -x.Amount)
            })
            .SingleOrDefaultAsync(ct);

        var rows = await q
            .OrderByDescending(x => x.At)
            .Take(2000)
            .Select(x => new CashReportRow(x.Id, x.At, x.Type, x.Amount, x.Note, x.RefType, x.RefId))
            .ToListAsync(ct);

        var summary = agg is null
            ? new CashReportSummary(0, 0, 0)
            : new CashReportSummary(agg.Net, agg.In, agg.Out);

        return Ok(new CashReportResponse(from, to, branchId, summary, rows));
    }

    [HttpGet("stock-ledger")]
    [RequirePermission(PermissionKeys.ReportsView)]
    public async Task<ActionResult<StockLedgerReportResponse>> StockLedger(
        [FromQuery] Guid branchId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] Guid? productId,
        [FromQuery] StockLedgerReason? reason,
        CancellationToken ct)
    {
        if (branchId == Guid.Empty)
        {
            return BadRequest(new { error = "BRANCH_REQUIRED" });
        }

        if (to < from)
        {
            return BadRequest(new { error = "INVALID_DATE_RANGE" });
        }

        if (productId == Guid.Empty)
        {
            productId = null;
        }

        var fromAt = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var toAt = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        var q = db.StockLedgers
            .AsNoTracking()
            .Where(x => x.BranchId == branchId && x.At >= fromAt && x.At < toAt);

        if (productId is not null)
        {
            q = q.Where(x => x.ProductId == productId.Value);
        }

        if (reason is not null)
        {
            q = q.Where(x => x.Reason == reason.Value);
        }

        var agg = await q
            .GroupBy(_ => 1)
            .Select(g => new
            {
                In = g.Where(x => x.QtyDelta > 0).Sum(x => x.QtyDelta),
                Out = g.Where(x => x.QtyDelta < 0).Sum(x => -x.QtyDelta),
                Net = g.Sum(x => x.QtyDelta)
            })
            .SingleOrDefaultAsync(ct);

        var rows = await q
            .Join(db.Products, l => l.ProductId, p => p.Id, (l, p) => new { l, p })
            .OrderByDescending(x => x.l.At)
            .Take(5000)
            .Select(x => new StockLedgerReportRow(
                x.l.At,
                x.l.ProductId,
                x.p.Name,
                x.l.QtyDelta,
                x.l.Reason,
                x.l.RefType,
                x.l.RefId))
            .ToListAsync(ct);

        var summary = agg is null
            ? new StockLedgerReportSummary(0, 0, 0)
            : new StockLedgerReportSummary(agg.In, agg.Out, agg.Net);

        return Ok(new StockLedgerReportResponse(from, to, branchId, productId, summary, rows));
    }
}
