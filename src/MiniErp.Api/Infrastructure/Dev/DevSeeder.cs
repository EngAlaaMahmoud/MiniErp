using Microsoft.EntityFrameworkCore;
using MiniErp.Api.Data;
using MiniErp.Api.Domain;
using MiniErp.Api.Domain.Enums;
using MiniErp.Api.Security;
using MiniErp.Api.Services;

namespace MiniErp.Api.Infrastructure.Dev;

public sealed class DevSeeder(IServiceProvider serviceProvider, ILogger<DevSeeder> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var pinHasher = scope.ServiceProvider.GetRequiredService<PinHasher>();

        await db.Database.MigrateAsync(cancellationToken);

        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var branchId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var deviceId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var ownerUserId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var catId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var vat14Id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var salesTaxTypeId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

        var tenantExists = await db.Tenants.AnyAsync(x => x.Id == tenantId, cancellationToken);
        if (!tenantExists)
        {
            db.Tenants.Add(new Tenant
            {
                Id = tenantId,
                Name = "Demo Shop",
                Plan = "DEV"
            });
        }

        var branchExists = await db.Branches.IgnoreQueryFilters().AnyAsync(x => x.Id == branchId, cancellationToken);
        if (!branchExists)
        {
            db.Branches.Add(new Branch
            {
                Id = branchId,
                TenantId = tenantId,
                Name = "Main Branch"
            });
        }

        var deviceExists = await db.Devices.IgnoreQueryFilters().AnyAsync(x => x.Id == deviceId, cancellationToken);
        if (!deviceExists)
        {
            db.Devices.Add(new Device
            {
                Id = deviceId,
                TenantId = tenantId,
                BranchId = branchId,
                DeviceKey = "DEV-DEVICE",
                LastSeenAt = DateTimeOffset.UtcNow
            });
        }

        var ownerExists = await db.Users.IgnoreQueryFilters().AnyAsync(x => x.Id == ownerUserId, cancellationToken);
        if (!ownerExists)
        {
            db.Users.Add(new User
            {
                Id = ownerUserId,
                TenantId = tenantId,
                Name = "owner",
                PinHash = pinHasher.HashPin("1234"),
                Role = UserRole.Owner,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        var hasAnyProducts = await db.Products.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId, cancellationToken);
        var hasAnyCategories = await db.Categories.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId, cancellationToken);
        if (!hasAnyCategories)
        {
            db.Categories.Add(new Category
            {
                Id = catId,
                TenantId = tenantId,
                Name = "عصير",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        var hasAnyRates = await db.TaxRates.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId, cancellationToken);
        if (!hasAnyRates)
        {
            db.TaxRates.Add(new TaxRate
            {
                Id = vat14Id,
                TenantId = tenantId,
                Name = "VAT 14%",
                Percent = 0.14m,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        if (!hasAnyProducts)
        {
            var milkId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
            var sugarId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

            db.Products.AddRange(
                new Product
                {
                    Id = milkId,
                    TenantId = tenantId,
                    Name = "Milk",
                    Sku = "MILK-1",
                    Cost = 25,
                    Price = 30,
                    CategoryId = catId,
                    TaxRateId = vat14Id,
                    SalesTaxTypeId = salesTaxTypeId,
                    ReorderLevel = 5,
                    IsActive = true
                },
                new Product
                {
                    Id = sugarId,
                    TenantId = tenantId,
                    Name = "Sugar",
                    Sku = "SUGAR-1",
                    Cost = 20,
                    Price = 25,
                    CategoryId = catId,
                    TaxRateId = vat14Id,
                    SalesTaxTypeId = salesTaxTypeId,
                    ReorderLevel = 5,
                    IsActive = true
                });

            db.ProductUnits.AddRange(
                new ProductUnit
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ProductId = milkId,
                    Name = "Unit",
                    Factor = 1m,
                    IsDefault = true,
                    IsActive = true,
                    CreatedAt = DateTimeOffset.UtcNow
                },
                new ProductUnit
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ProductId = sugarId,
                    Name = "Unit",
                    Factor = 1m,
                    IsDefault = true,
                    IsActive = true,
                    CreatedAt = DateTimeOffset.UtcNow
                });

            db.Barcodes.AddRange(
                new Barcode { Id = Guid.NewGuid(), TenantId = tenantId, ProductId = milkId, Code = "622000000001" },
                new Barcode { Id = Guid.NewGuid(), TenantId = tenantId, ProductId = sugarId, Code = "622000000002" });
        }
        else
        {
            var productIds = await db.Products.IgnoreQueryFilters()
                .Where(x => x.TenantId == tenantId)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

            var unitProductIds = await db.ProductUnits.IgnoreQueryFilters()
                .Where(x => x.TenantId == tenantId)
                .Select(x => x.ProductId)
                .Distinct()
                .ToListAsync(cancellationToken);

            foreach (var productId in productIds.Except(unitProductIds))
            {
                db.ProductUnits.Add(new ProductUnit
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ProductId = productId,
                    Name = "Unit",
                    Factor = 1m,
                    IsDefault = true,
                    IsActive = true,
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }
        }

        var hasAnyTaxes = await db.SalesTaxTypes.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId, cancellationToken);
        if (!hasAnyTaxes)
        {
            db.SalesTaxTypes.Add(new SalesTaxType
            {
                Id = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
                TenantId = tenantId,
                MainCode = "T_1",
                SubCode = "V009",
                TaxType = "VAT",
                Description = "سلع عامة",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            });

            db.SalesTaxTypes.AddRange(
                new SalesTaxType
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    MainCode = "T_4",
                    SubCode = "W013",
                    TaxType = "WHT",
                    Description = "إتاوات",
                    IsActive = true,
                    CreatedAt = DateTimeOffset.UtcNow
                });
        }

        var hasAnySuppliers = await db.Suppliers.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId, cancellationToken);
        if (!hasAnySuppliers)
        {
            db.Suppliers.Add(new Supplier
            {
                Id = Guid.Parse("55555555-5555-5555-5555-555555555555"),
                TenantId = tenantId,
                Name = "Demo Supplier",
                Phone = "01000000000",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        var hasAnyRolePerms = await db.RolePermissions.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId, cancellationToken);
        if (!hasAnyRolePerms)
        {
            var all = PermissionCatalog.All.Select(x => x.Key).ToArray();
            db.RolePermissions.AddRange(all.Select(k => new RolePermission { TenantId = tenantId, Role = UserRole.Owner, PermissionKey = k }));

            var manager = new[]
            {
                PermissionKeys.DashboardView,
                PermissionKeys.ReportsView,
                PermissionKeys.AccountsView,
                PermissionKeys.AccountsManage,
                PermissionKeys.CatalogView,
                PermissionKeys.CatalogEdit,
                PermissionKeys.InventoryView,
                PermissionKeys.InventoryAdjust,
                PermissionKeys.TaxesView,
                PermissionKeys.TaxesManage,
                PermissionKeys.CustomersView,
                PermissionKeys.CustomersEdit,
                PermissionKeys.SuppliersView,
                PermissionKeys.SuppliersEdit,
                PermissionKeys.PurchasesView,
                PermissionKeys.PurchasesCreate,
                PermissionKeys.PurchasesPrint,
                PermissionKeys.SalesView,
                PermissionKeys.SalesCreate,
                PermissionKeys.SalesPrint,
                PermissionKeys.ReturnsCreate,
                PermissionKeys.AdminPermissions
            };
            db.RolePermissions.AddRange(manager.Select(k => new RolePermission { TenantId = tenantId, Role = UserRole.Manager, PermissionKey = k }));

            var cashier = new[]
            {
                PermissionKeys.DashboardView,
                PermissionKeys.SalesView,
                PermissionKeys.SalesCreate,
                PermissionKeys.SalesPrint,
                PermissionKeys.ReturnsCreate,
                PermissionKeys.CustomersView,
                PermissionKeys.SuppliersView
            };
            db.RolePermissions.AddRange(cashier.Select(k => new RolePermission { TenantId = tenantId, Role = UserRole.Cashier, PermissionKey = k }));

            await db.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation(
            "Dev seed ready. TenantId={TenantId} BranchId={BranchId} DeviceId={DeviceId} User=owner PIN=1234",
            tenantId, branchId, deviceId);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
