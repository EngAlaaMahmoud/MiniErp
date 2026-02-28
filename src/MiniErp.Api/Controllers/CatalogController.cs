using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniErp.Api.Contracts.Catalog;
using MiniErp.Api.Data;
using MiniErp.Api.Domain;
using MiniErp.Api.Security;

namespace MiniErp.Api.Controllers;

[ApiController]
[Route("catalog")]
[Authorize]
public sealed class CatalogController(AppDbContext db) : ControllerBase
{
    [HttpGet("products")]
    [RequirePermission(PermissionKeys.CatalogView)]
    public async Task<ActionResult<IReadOnlyList<ProductListItem>>> GetProducts([FromQuery] string? search, CancellationToken ct)
    {
        var tenantId = db.TenantId;
        if (tenantId == Guid.Empty)
        {
            return BadRequest(new { error = "TENANT_REQUIRED" });
        }

        IQueryable<Product> query = db.Products;
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x => x.Name.Contains(search) || (x.Sku != null && x.Sku.Contains(search)));
        }

        var items = await query
            .OrderBy(x => x.Name)
            .Take(200)
            .Select(x => new ProductListItem(x.Id, x.Name, x.Sku, x.Price, x.Cost, x.CategoryId, x.TaxRateId, x.SalesTaxTypeId, x.ReorderLevel, x.IsActive))
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpPost("products")]
    [RequirePermission(PermissionKeys.CatalogEdit)]
    public async Task<IActionResult> CreateProduct([FromBody] CreateProductRequest request, CancellationToken ct)
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

        var product = new Product
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = request.Name.Trim(),
            Sku = string.IsNullOrWhiteSpace(request.Sku) ? null : request.Sku.Trim(),
            Price = request.Price,
            Cost = request.Cost,
            CategoryId = request.CategoryId,
            TaxRateId = request.TaxRateId,
            SalesTaxTypeId = request.SalesTaxTypeId,
            ReorderLevel = request.ReorderLevel,
            IsActive = request.IsActive
        };

        db.Products.Add(product);
        db.ProductUnits.Add(new ProductUnit
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProductId = product.Id,
            Name = "Unit",
            Factor = 1m,
            IsDefault = true,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetProducts), new { id = product.Id }, new ProductListItem(product.Id, product.Name, product.Sku, product.Price, product.Cost, product.CategoryId, product.TaxRateId, product.SalesTaxTypeId, product.ReorderLevel, product.IsActive));
    }

    [HttpPut("products/{id:guid}")]
    [RequirePermission(PermissionKeys.CatalogEdit)]
    public async Task<IActionResult> UpdateProduct([FromRoute] Guid id, [FromBody] UpdateProductRequest request, CancellationToken ct)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new { error = "INVALID_ID" });
        }

        var product = await db.Products.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (product is null)
        {
            return NotFound(new { error = "NOT_FOUND" });
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { error = "NAME_REQUIRED" });
        }

        product.Name = request.Name.Trim();
        product.Sku = string.IsNullOrWhiteSpace(request.Sku) ? null : request.Sku.Trim();
        product.Price = request.Price;
        product.Cost = request.Cost;
        product.CategoryId = request.CategoryId;
        product.TaxRateId = request.TaxRateId;
        product.SalesTaxTypeId = request.SalesTaxTypeId;
        product.ReorderLevel = request.ReorderLevel;
        product.IsActive = request.IsActive;

        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("products/{productId:guid}/units")]
    [RequirePermission(PermissionKeys.CatalogView)]
    public async Task<ActionResult<IReadOnlyList<ProductUnitListItem>>> GetProductUnits([FromRoute] Guid productId, CancellationToken ct)
    {
        if (productId == Guid.Empty)
        {
            return BadRequest(new { error = "INVALID_PRODUCT" });
        }

        var exists = await db.Products.AnyAsync(x => x.Id == productId, ct);
        if (!exists)
        {
            return NotFound(new { error = "PRODUCT_NOT_FOUND" });
        }

        var units = await db.ProductUnits
            .Where(x => x.ProductId == productId)
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.Name)
            .Select(x => new ProductUnitListItem(x.Id, x.ProductId, x.Name, x.Factor, x.IsDefault, x.IsActive))
            .ToListAsync(ct);

        return Ok(units);
    }

    [HttpPost("products/{productId:guid}/units")]
    [RequirePermission(PermissionKeys.CatalogEdit)]
    public async Task<IActionResult> CreateProductUnit([FromRoute] Guid productId, [FromBody] CreateProductUnitRequest request, CancellationToken ct)
    {
        var tenantId = db.TenantId;
        if (tenantId == Guid.Empty)
        {
            return BadRequest(new { error = "TENANT_REQUIRED" });
        }

        if (productId == Guid.Empty)
        {
            return BadRequest(new { error = "INVALID_PRODUCT" });
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { error = "NAME_REQUIRED" });
        }

        if (request.Factor <= 0)
        {
            return BadRequest(new { error = "INVALID_FACTOR" });
        }

        var productExists = await db.Products.AnyAsync(x => x.Id == productId, ct);
        if (!productExists)
        {
            return NotFound(new { error = "PRODUCT_NOT_FOUND" });
        }

        if (request.IsDefault)
        {
            var currentDefaults = await db.ProductUnits.Where(x => x.ProductId == productId && x.IsDefault).ToListAsync(ct);
            foreach (var u in currentDefaults)
            {
                u.IsDefault = false;
            }
        }

        var unit = new ProductUnit
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProductId = productId,
            Name = request.Name.Trim(),
            Factor = request.Factor,
            IsDefault = request.IsDefault,
            IsActive = request.IsActive,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.ProductUnits.Add(unit);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(new { error = "DUPLICATE_NAME" });
        }

        return CreatedAtAction(nameof(GetProductUnits), new { productId }, new ProductUnitListItem(unit.Id, unit.ProductId, unit.Name, unit.Factor, unit.IsDefault, unit.IsActive));
    }

    [HttpPut("products/{productId:guid}/units/{unitId:guid}")]
    [RequirePermission(PermissionKeys.CatalogEdit)]
    public async Task<IActionResult> UpdateProductUnit([FromRoute] Guid productId, [FromRoute] Guid unitId, [FromBody] UpdateProductUnitRequest request, CancellationToken ct)
    {
        if (productId == Guid.Empty || unitId == Guid.Empty)
        {
            return BadRequest(new { error = "INVALID_ID" });
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { error = "NAME_REQUIRED" });
        }

        if (request.Factor <= 0)
        {
            return BadRequest(new { error = "INVALID_FACTOR" });
        }

        var unit = await db.ProductUnits.SingleOrDefaultAsync(x => x.Id == unitId && x.ProductId == productId, ct);
        if (unit is null)
        {
            return NotFound(new { error = "NOT_FOUND" });
        }

        if (request.IsDefault)
        {
            var currentDefaults = await db.ProductUnits.Where(x => x.ProductId == productId && x.IsDefault && x.Id != unitId).ToListAsync(ct);
            foreach (var u in currentDefaults)
            {
                u.IsDefault = false;
            }
        }

        unit.Name = request.Name.Trim();
        unit.Factor = request.Factor;
        unit.IsDefault = request.IsDefault;
        unit.IsActive = request.IsActive;

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

    [HttpGet("categories")]
    [RequirePermission(PermissionKeys.CatalogView)]
    public async Task<ActionResult<IReadOnlyList<CategoryListItem>>> GetCategories(CancellationToken ct)
    {
        var tenantId = db.TenantId;
        if (tenantId == Guid.Empty)
        {
            return BadRequest(new { error = "TENANT_REQUIRED" });
        }

        var items = await db.Categories
            .OrderBy(x => x.Name)
            .Take(500)
            .Select(x => new CategoryListItem(x.Id, x.Name, x.IsActive))
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpPost("categories")]
    [RequirePermission(PermissionKeys.CatalogEdit)]
    public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryRequest request, CancellationToken ct)
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

        var entity = new Category
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = request.Name.Trim(),
            IsActive = request.IsActive,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.Categories.Add(entity);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(new { error = "DUPLICATE_NAME" });
        }

        return CreatedAtAction(nameof(GetCategories), new { id = entity.Id }, new CategoryListItem(entity.Id, entity.Name, entity.IsActive));
    }

    [HttpPut("categories/{id:guid}")]
    [RequirePermission(PermissionKeys.CatalogEdit)]
    public async Task<IActionResult> UpdateCategory([FromRoute] Guid id, [FromBody] UpdateCategoryRequest request, CancellationToken ct)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new { error = "INVALID_ID" });
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { error = "NAME_REQUIRED" });
        }

        var entity = await db.Categories.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
        {
            return NotFound(new { error = "NOT_FOUND" });
        }

        entity.Name = request.Name.Trim();
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

    [HttpPost("products/{productId:guid}/barcodes")]
    [RequirePermission(PermissionKeys.CatalogEdit)]
    public async Task<IActionResult> AddBarcode([FromRoute] Guid productId, [FromBody] AddBarcodeRequest request, CancellationToken ct)
    {
        var tenantId = db.TenantId;
        if (tenantId == Guid.Empty)
        {
            return BadRequest(new { error = "TENANT_REQUIRED" });
        }

        if (productId == Guid.Empty)
        {
            return BadRequest(new { error = "INVALID_PRODUCT" });
        }

        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return BadRequest(new { error = "CODE_REQUIRED" });
        }

        var productExists = await db.Products.AnyAsync(x => x.Id == productId, ct);
        if (!productExists)
        {
            return NotFound(new { error = "PRODUCT_NOT_FOUND" });
        }

        var barcode = new Barcode
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProductId = productId,
            Code = request.Code.Trim()
        };
        db.Barcodes.Add(barcode);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(new { error = "BARCODE_ALREADY_EXISTS" });
        }

        return Created("", new { barcode.Id, barcode.Code, barcode.ProductId });
    }

    [HttpGet("lookup/barcode/{code}")]
    [RequirePermission(PermissionKeys.CatalogView)]
    public async Task<IActionResult> LookupBarcode([FromRoute] string code, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return BadRequest(new { error = "CODE_REQUIRED" });
        }

        var result = await db.Barcodes
            .Where(x => x.Code == code)
            .Join(db.Products, b => b.ProductId, p => p.Id, (b, p) => new { b.Code, p.Id, p.Name, p.Price })
            .SingleOrDefaultAsync(ct);

        if (result is null)
        {
            return NotFound(new { error = "NOT_FOUND" });
        }

        return Ok(new BarcodeLookupResponse(result.Id, result.Name, result.Price, result.Code));
    }
}
