using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniErp.Api.Contracts.Parties;
using MiniErp.Api.Data;
using MiniErp.Api.Domain;
using MiniErp.Api.Security;

namespace MiniErp.Api.Controllers;

[ApiController]
[Route("customers")]
[Authorize]
public sealed class CustomersController(AppDbContext db) : ControllerBase
{
    [HttpGet("lookup/countries")]
    public async Task<ActionResult<IReadOnlyList<CountryOption>>> GetCountries(CancellationToken ct)
    {
        var countries = await db.Countries
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.NameAr)
            .Select(x => new CountryOption(x.Id, x.Name, x.NameAr))
            .ToListAsync(ct);

        return Ok(countries);
    }

    [HttpGet("lookup/governorates/{countryId:guid}")]
    public async Task<ActionResult<IReadOnlyList<GovernorateOption>>> GetGovernoratesByCountry([FromRoute] Guid countryId, CancellationToken ct)
    {
        var governorates = await db.Governorates
            .AsNoTracking()
            .Where(x => x.CountryId == countryId && x.IsActive)
            .OrderBy(x => x.NameAr)
            .Select(x => new GovernorateOption(x.Id, x.CountryId, x.Name, x.NameAr))
            .ToListAsync(ct);

        return Ok(governorates);
    }

    [HttpGet("lookup/customer-types")]
    public async Task<ActionResult<IReadOnlyList<CustomerTypeOption>>> GetCustomerTypes(CancellationToken ct)
    {
        var types = await db.CustomerTypes
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.NameAr)
            .Select(x => new CustomerTypeOption(x.Id, x.Name, x.NameAr))
            .ToListAsync(ct);

        return Ok(types);
    }

    [HttpGet]
    [RequirePermission(PermissionKeys.CustomersView)]
    public async Task<ActionResult<IReadOnlyList<CustomerListItem>>> List([FromQuery] string? search, CancellationToken ct)
    {
        var tenantId = db.TenantId;
        if (tenantId == Guid.Empty)
        {
            return BadRequest(new { error = "TENANT_REQUIRED" });
        }

        IQueryable<Customer> q = db.Customers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            q = q.Where(x => x.Name.Contains(search) || (x.Phone != null && x.Phone.Contains(search)));
        }

        // fetch customers (limit)
        var customers = await q
            .OrderBy(x => x.Name)
            .Take(500)
            .ToListAsync(ct);

        // collect referenced type/country/gov ids so we can return names where possible
        var typeIds = customers.Where(x => x.CustomerTypeId.HasValue).Select(x => x.CustomerTypeId!.Value).Distinct().ToArray();
        var countryIds = customers.Where(x => x.CountryId.HasValue).Select(x => x.CountryId!.Value).Distinct().ToArray();
        var govIds = customers.Where(x => x.GovernorateId.HasValue).Select(x => x.GovernorateId!.Value).Distinct().ToArray();

        var types = typeIds.Length == 0 ? new List<CustomerType>() : await db.CustomerTypes.AsNoTracking().Where(x => typeIds.Contains(x.Id)).ToListAsync(ct);
        var countries = countryIds.Length == 0 ? new List<Country>() : await db.Countries.AsNoTracking().Where(x => countryIds.Contains(x.Id)).ToListAsync(ct);
        var governorates = govIds.Length == 0 ? new List<Governorate>() : await db.Governorates.AsNoTracking().Where(x => govIds.Contains(x.Id)).ToListAsync(ct);

        var typesById = types.ToDictionary(x => x.Id, x => x);
        var countriesById = countries.ToDictionary(x => x.Id, x => x);
        var govsById = governorates.ToDictionary(x => x.Id, x => x);

        var items = customers
            .Select(x => new CustomerListItem(
                x.Id,
                x.Name,
                x.Phone,
                x.TaxRegistrationNo,
                x.CustomerTypeId,
                x.CustomerTypeId.HasValue && typesById.TryGetValue(x.CustomerTypeId.Value, out var t) ? t.NameAr : null,
                x.CountryId,
                x.CountryId.HasValue && countriesById.TryGetValue(x.CountryId.Value, out var c) ? c.NameAr : x.Country,
                x.GovernorateId,
                x.GovernorateId.HasValue && govsById.TryGetValue(x.GovernorateId.Value, out var g) ? g.NameAr : x.Governorate,
                x.City,
                x.BuildingNo,
                x.Floor,
                x.Apartment,
                x.StreetName,
                x.PostalCode,
                x.Address,
                x.IsActive))
            .ToList()
            .AsReadOnly();

        return Ok(items);
    }

    [HttpPost]
    [RequirePermission(PermissionKeys.CustomersEdit)]
    public async Task<IActionResult> Create([FromBody] CreateCustomerRequest request, CancellationToken ct)
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
            var dup = await db.Customers.AnyAsync(x => x.TaxRegistrationNo == taxNo, ct);
            if (dup)
            {
                return Conflict(new { error = "DUPLICATE_TAX_NUMBER" });
            }
        }

        // resolve country/governorate names when IDs are provided or use text fallback
        string? countryName = null;
        if (request.CountryId.HasValue)
        {
            var c = await db.Countries.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.CountryId.Value, ct);
            countryName = c is null ? null : (string.IsNullOrWhiteSpace(c.NameAr) ? c.Name : c.NameAr);
        }

        string? governorateName = null;
        if (request.GovernorateId.HasValue)
        {
            var g = await db.Governorates.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.GovernorateId.Value, ct);
            governorateName = g is null ? null : (string.IsNullOrWhiteSpace(g.NameAr) ? g.Name : g.NameAr);
        }
        // fallback to provided free text
        if (string.IsNullOrWhiteSpace(governorateName) && !string.IsNullOrWhiteSpace(request.GovernorateText))
        {
            governorateName = request.GovernorateText.Trim();
        }

        var entity = new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = request.Name.Trim(),
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            TaxRegistrationNo = taxNo,
            CustomerTypeId = request.CustomerTypeId,
            CountryId = request.CountryId,
            GovernorateId = request.GovernorateId,
            Country = countryName,
            Governorate = governorateName,
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
        db.Customers.Add(entity);
        await db.SaveChangesAsync(ct);

        var customerType = entity.CustomerTypeId.HasValue ? await db.CustomerTypes.SingleOrDefaultAsync(x => x.Id == entity.CustomerTypeId, ct) : null;

        return CreatedAtAction(nameof(List), new { id = entity.Id }, new CustomerListItem(
            entity.Id,
            entity.Name,
            entity.Phone,
            entity.TaxRegistrationNo,
            entity.CustomerTypeId,
            customerType?.NameAr,
            entity.CountryId,
            entity.Country,
            entity.GovernorateId,
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
    [RequirePermission(PermissionKeys.CustomersEdit)]
    public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateCustomerRequest request, CancellationToken ct)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new { error = "INVALID_ID" });
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { error = "NAME_REQUIRED" });
        }

        var entity = await db.Customers.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
        {
            return NotFound(new { error = "NOT_FOUND" });
        }

        var taxNo = string.IsNullOrWhiteSpace(request.TaxRegistrationNo) ? null : request.TaxRegistrationNo.Trim();
        if (!string.IsNullOrWhiteSpace(taxNo))
        {
            var dup = await db.Customers.AnyAsync(x => x.Id != id && x.TaxRegistrationNo == taxNo, ct);
            if (dup)
            {
                return Conflict(new { error = "DUPLICATE_TAX_NUMBER" });
            }
        }

        // resolve country/governorate names when IDs are provided or use text fallback
        string? countryName = null;
        if (request.CountryId.HasValue)
        {
            var c = await db.Countries.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.CountryId.Value, ct);
            countryName = c is null ? null : (string.IsNullOrWhiteSpace(c.NameAr) ? c.Name : c.NameAr);
        }

        string? governorateName = null;
        if (request.GovernorateId.HasValue)
        {
            var g = await db.Governorates.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.GovernorateId.Value, ct);
            governorateName = g is null ? null : (string.IsNullOrWhiteSpace(g.NameAr) ? g.Name : g.NameAr);
        }
        // fallback to provided free text
        if (string.IsNullOrWhiteSpace(governorateName) && !string.IsNullOrWhiteSpace(request.GovernorateText))
        {
            governorateName = request.GovernorateText.Trim();
        }

        entity.Name = request.Name.Trim();
        entity.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        entity.TaxRegistrationNo = taxNo;
        entity.CustomerTypeId = request.CustomerTypeId;
        entity.CountryId = request.CountryId;
        entity.GovernorateId = request.GovernorateId;
        entity.Country = countryName;
        entity.Governorate = governorateName;
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