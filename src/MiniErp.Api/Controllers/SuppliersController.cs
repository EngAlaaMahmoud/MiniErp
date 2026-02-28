using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniErp.Api.Contracts.Parties;
using MiniErp.Api.Data;
using MiniErp.Api.Domain;
using MiniErp.Api.Security;

namespace MiniErp.Api.Controllers;

[ApiController]
[Route("suppliers")]
[Authorize]
public sealed class SuppliersController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionKeys.SuppliersView)]
    public async Task<ActionResult<IReadOnlyList<SupplierListItem>>> List([FromQuery] string? search, CancellationToken ct)
    {
        var tenantId = db.TenantId;
        if (tenantId == Guid.Empty)
        {
            return BadRequest(new { error = "TENANT_REQUIRED" });
        }

        IQueryable<Supplier> query = db.Suppliers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x => x.Name.Contains(search) || (x.Phone != null && x.Phone.Contains(search)));
        }

        var items = await query
            .OrderBy(x => x.Name)
            .Take(500)
            .Select(x => new SupplierListItem(x.Id, x.Name, x.Phone, x.IsActive))
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpPost]
    [RequirePermission(PermissionKeys.SuppliersEdit)]
    public async Task<IActionResult> Create([FromBody] CreateSupplierRequest request, CancellationToken ct)
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

        var entity = new Supplier
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = request.Name.Trim(),
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            IsActive = request.IsActive,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Suppliers.Add(entity);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(List), new { id = entity.Id }, new SupplierListItem(entity.Id, entity.Name, entity.Phone, entity.IsActive));
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionKeys.SuppliersEdit)]
    public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateSupplierRequest request, CancellationToken ct)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new { error = "INVALID_ID" });
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { error = "NAME_REQUIRED" });
        }

        var entity = await db.Suppliers.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
        {
            return NotFound(new { error = "NOT_FOUND" });
        }

        entity.Name = request.Name.Trim();
        entity.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        entity.IsActive = request.IsActive;

        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
