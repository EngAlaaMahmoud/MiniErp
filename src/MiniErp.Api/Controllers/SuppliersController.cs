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
            .Select(x => new SupplierListItem(
                x.Id,
                x.Name,
                x.Phone,
                x.TaxRegistrationNo,
                x.Country,
                x.Governorate,
                x.City,
                x.BuildingNo,
                x.Floor,
                x.Apartment,
                x.StreetName,
                x.PostalCode,
                x.Address,
                x.IsActive))
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

        var taxNo = string.IsNullOrWhiteSpace(request.TaxRegistrationNo) ? null : request.TaxRegistrationNo.Trim();
        if (!string.IsNullOrWhiteSpace(taxNo))
        {
            var dup = await db.Suppliers.AnyAsync(x => x.TaxRegistrationNo == taxNo, ct);
            if (dup)
            {
                return Conflict(new { error = "DUPLICATE_TAX_NUMBER" });
            }
        }

        var entity = new Supplier
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = request.Name.Trim(),
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            TaxRegistrationNo = taxNo,
            Country = string.IsNullOrWhiteSpace(request.Country) ? null : request.Country.Trim(),
            Governorate = string.IsNullOrWhiteSpace(request.Governorate) ? null : request.Governorate.Trim(),
            City = string.IsNullOrWhiteSpace(request.City) ? null : request.City.Trim(),
            BuildingNo = string.IsNullOrWhiteSpace(request.BuildingNo) ? null : request.BuildingNo.Trim(),
            Floor = string.IsNullOrWhiteSpace(request.Floor) ? null : request.Floor.Trim(),
            Apartment = string.IsNullOrWhiteSpace(request.Apartment) ? null : request.Apartment.Trim(),
            StreetName = string.IsNullOrWhiteSpace(request.StreetName) ? null : request.StreetName.Trim(),
            PostalCode = string.IsNullOrWhiteSpace(request.PostalCode) ? null : request.PostalCode.Trim(),
            Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim(),
            IsActive = request.IsActive,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Suppliers.Add(entity);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(List), new { id = entity.Id }, new SupplierListItem(
            entity.Id,
            entity.Name,
            entity.Phone,
            entity.TaxRegistrationNo,
            entity.Country,
            entity.Governorate,
            entity.City,
            entity.BuildingNo,
            entity.Floor,
            entity.Apartment,
            entity.StreetName,
            entity.PostalCode,
            entity.Address,
            entity.IsActive));
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

        var taxNo = string.IsNullOrWhiteSpace(request.TaxRegistrationNo) ? null : request.TaxRegistrationNo.Trim();
        if (!string.IsNullOrWhiteSpace(taxNo))
        {
            var dup = await db.Suppliers.AnyAsync(x => x.Id != id && x.TaxRegistrationNo == taxNo, ct);
            if (dup)
            {
                return Conflict(new { error = "DUPLICATE_TAX_NUMBER" });
            }
        }

        entity.Name = request.Name.Trim();
        entity.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        entity.TaxRegistrationNo = taxNo;
        entity.Country = string.IsNullOrWhiteSpace(request.Country) ? null : request.Country.Trim();
        entity.Governorate = string.IsNullOrWhiteSpace(request.Governorate) ? null : request.Governorate.Trim();
        entity.City = string.IsNullOrWhiteSpace(request.City) ? null : request.City.Trim();
        entity.BuildingNo = string.IsNullOrWhiteSpace(request.BuildingNo) ? null : request.BuildingNo.Trim();
        entity.Floor = string.IsNullOrWhiteSpace(request.Floor) ? null : request.Floor.Trim();
        entity.Apartment = string.IsNullOrWhiteSpace(request.Apartment) ? null : request.Apartment.Trim();
        entity.StreetName = string.IsNullOrWhiteSpace(request.StreetName) ? null : request.StreetName.Trim();
        entity.PostalCode = string.IsNullOrWhiteSpace(request.PostalCode) ? null : request.PostalCode.Trim();
        entity.Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();
        entity.IsActive = request.IsActive;

        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
