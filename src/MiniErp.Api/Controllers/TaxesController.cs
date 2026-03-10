using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniErp.Api.Contracts.Taxes;
using MiniErp.Api.Data;
using MiniErp.Api.Domain;
using MiniErp.Api.Security;

namespace MiniErp.Api.Controllers;

[ApiController]
[Route("taxes")]
[Authorize]
public sealed class TaxesController(AppDbContext db) : ControllerBase
{
    [HttpGet("types")]
    [RequirePermission(PermissionKeys.TaxesView)]
    public async Task<ActionResult<IReadOnlyList<TaxTypeListItem>>> GetTaxTypes([FromQuery] string? search, CancellationToken ct)
    {
        var tenantId = db.TenantId;
        if (tenantId == Guid.Empty)
        {
            return BadRequest(new { error = "TENANT_REQUIRED" });
        }

        IQueryable<SalesTaxType> query = db.SalesTaxTypes;
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x =>
                x.MainCode.Contains(search) ||
                x.SubCode.Contains(search) ||
                x.TaxType.Contains(search) ||
                x.Description.Contains(search));
        }

        var items = await query
            .OrderBy(x => x.MainCode)
            .ThenBy(x => x.SubCode)
            .Take(500)
            .Select(x => new TaxTypeListItem(x.Id, x.MainCode, x.SubCode, x.TaxType, x.Description, x.Percent, x.IsActive))
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpPost("types")]
    [RequirePermission(PermissionKeys.TaxesManage)]
    public async Task<IActionResult> CreateTaxType([FromBody] CreateTaxTypeRequest request, CancellationToken ct)
    {
        var tenantId = db.TenantId;
        if (tenantId == Guid.Empty)
        {
            return BadRequest(new { error = "TENANT_REQUIRED" });
        }

        if (string.IsNullOrWhiteSpace(request.MainCode) ||
            string.IsNullOrWhiteSpace(request.SubCode) ||
            string.IsNullOrWhiteSpace(request.TaxType) ||
            string.IsNullOrWhiteSpace(request.Description))
        {
            return BadRequest(new { error = "INVALID_REQUEST" });
        }

        if (request.Percent < 0 || request.Percent > 1)
        {
            return BadRequest(new { error = "INVALID_PERCENT" });
        }

        var entity = new SalesTaxType
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            MainCode = request.MainCode.Trim(),
            SubCode = request.SubCode.Trim(),
            TaxType = request.TaxType.Trim(),
            Description = request.Description.Trim(),
            Percent = request.Percent,
            IsActive = request.IsActive,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.SalesTaxTypes.Add(entity);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(new { error = "DUPLICATE_CODE" });
        }

        return CreatedAtAction(nameof(GetTaxTypes), new { id = entity.Id }, new TaxTypeListItem(entity.Id, entity.MainCode, entity.SubCode, entity.TaxType, entity.Description, entity.Percent, entity.IsActive));
    }

    [HttpPut("types/{id:guid}")]
    [RequirePermission(PermissionKeys.TaxesManage)]
    public async Task<IActionResult> UpdateTaxType([FromRoute] Guid id, [FromBody] UpdateTaxTypeRequest request, CancellationToken ct)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new { error = "INVALID_ID" });
        }

        if (string.IsNullOrWhiteSpace(request.MainCode) ||
            string.IsNullOrWhiteSpace(request.SubCode) ||
            string.IsNullOrWhiteSpace(request.TaxType) ||
            string.IsNullOrWhiteSpace(request.Description))
        {
            return BadRequest(new { error = "INVALID_REQUEST" });
        }

        if (request.Percent < 0 || request.Percent > 1)
        {
            return BadRequest(new { error = "INVALID_PERCENT" });
        }

        var entity = await db.SalesTaxTypes.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
        {
            return NotFound(new { error = "NOT_FOUND" });
        }

        entity.MainCode = request.MainCode.Trim();
        entity.SubCode = request.SubCode.Trim();
        entity.TaxType = request.TaxType.Trim();
        entity.Description = request.Description.Trim();
        entity.Percent = request.Percent;
        entity.IsActive = request.IsActive;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(new { error = "DUPLICATE_CODE" });
        }

        return NoContent();
    }

    [HttpGet("rates")]
    [RequirePermission(PermissionKeys.TaxesView)]
    public async Task<ActionResult<IReadOnlyList<TaxRateListItem>>> GetTaxRates([FromQuery] string? search, CancellationToken ct)
    {
        var tenantId = db.TenantId;
        if (tenantId == Guid.Empty)
        {
            return BadRequest(new { error = "TENANT_REQUIRED" });
        }

        IQueryable<TaxRate> query = db.TaxRates;
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x => x.Name.Contains(search));
        }

        var items = await query
            .OrderBy(x => x.Name)
            .Take(200)
            .Select(x => new TaxRateListItem(x.Id, x.Name, x.Percent, x.IsActive))
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpPost("rates")]
    [RequirePermission(PermissionKeys.TaxesManage)]
    public async Task<IActionResult> CreateTaxRate([FromBody] CreateTaxRateRequest request, CancellationToken ct)
    {
        var tenantId = db.TenantId;
        if (tenantId == Guid.Empty)
        {
            return BadRequest(new { error = "TENANT_REQUIRED" });
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { error = "NAME_REQUIRED" });
        }

        if (request.Percent < 0 || request.Percent > 1)
        {
            return BadRequest(new { error = "INVALID_PERCENT" });
        }

        var entity = new TaxRate
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = request.Name.Trim(),
            Percent = request.Percent,
            IsActive = request.IsActive,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.TaxRates.Add(entity);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(new { error = "DUPLICATE_NAME" });
        }

        return CreatedAtAction(nameof(GetTaxRates), new { id = entity.Id }, new TaxRateListItem(entity.Id, entity.Name, entity.Percent, entity.IsActive));
    }

    [HttpPut("rates/{id:guid}")]
    [RequirePermission(PermissionKeys.TaxesManage)]
    public async Task<IActionResult> UpdateTaxRate([FromRoute] Guid id, [FromBody] UpdateTaxRateRequest request, CancellationToken ct)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new { error = "INVALID_ID" });
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { error = "NAME_REQUIRED" });
        }

        if (request.Percent < 0 || request.Percent > 1)
        {
            return BadRequest(new { error = "INVALID_PERCENT" });
        }

        var entity = await db.TaxRates.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
        {
            return NotFound(new { error = "NOT_FOUND" });
        }

        entity.Name = request.Name.Trim();
        entity.Percent = request.Percent;
        entity.IsActive = request.IsActive;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(new { error = "DUPLICATE_NAME" });
        }

        return NoContent();
    }

    [HttpGet("report")]
    [RequirePermission(PermissionKeys.TaxesView)]
    public async Task<ActionResult<TaxSummaryResponse>> GetTaxReport([FromQuery] Guid? branchId, [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, CancellationToken ct)
    {
        var tenantId = db.TenantId;
        if (tenantId == Guid.Empty)
        {
            return BadRequest(new { error = "TENANT_REQUIRED" });
        }

        var fromUtc = (from ?? DateTimeOffset.UtcNow.AddDays(-30)).ToUniversalTime();
        var toUtc = (to ?? DateTimeOffset.UtcNow).ToUniversalTime();
        if (toUtc <= fromUtc)
        {
            return BadRequest(new { error = "INVALID_RANGE" });
        }

        var salesTax = await (
            from si in db.SaleItems
            join s in db.Sales on si.SaleId equals s.Id
            where s.At >= fromUtc && s.At < toUtc
            where branchId == null || s.BranchId == branchId
            select (decimal?)si.TaxAmount
        ).SumAsync(ct) ?? 0m;

        var returnsTax = await (
            from ri in db.ReturnItems
            join r in db.Returns on ri.ReturnId equals r.Id
            where r.At >= fromUtc && r.At < toUtc
            where branchId == null || r.BranchId == branchId
            select (decimal?)ri.TaxAmount
        ).SumAsync(ct) ?? 0m;

        var purchaseTax = await (
            from pi in db.PurchaseItems
            join p in db.Purchases on pi.PurchaseId equals p.Id
            where p.At >= fromUtc && p.At < toUtc
            where branchId == null || p.BranchId == branchId
            select (decimal?)pi.TaxAmount
        ).SumAsync(ct) ?? 0m;

        var netSalesTax = salesTax - returnsTax;
        var net = netSalesTax - purchaseTax;

        return Ok(new TaxSummaryResponse(fromUtc, toUtc, branchId, netSalesTax, purchaseTax, net));
    }
}
