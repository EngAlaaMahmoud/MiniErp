using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniErp.Api.Contracts.Catalog;
using MiniErp.Api.Data;
using MiniErp.Api.Domain;

namespace MiniErp.Api.Controllers;

[ApiController]
[Route("catalog")]
[Authorize]
public sealed class CatalogController(AppDbContext db) : ControllerBase
{
    [HttpGet("products")]
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
            .Select(x => new ProductListItem(x.Id, x.Name, x.Sku, x.Price, x.Cost, x.IsActive))
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpPost("products")]
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
            IsActive = request.IsActive
        };

        db.Products.Add(product);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetProducts), new { id = product.Id }, new ProductListItem(product.Id, product.Name, product.Sku, product.Price, product.Cost, product.IsActive));
    }

    [HttpPut("products/{id:guid}")]
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
        product.IsActive = request.IsActive;

        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("products/{productId:guid}/barcodes")]
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

