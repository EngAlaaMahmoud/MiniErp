using Microsoft.EntityFrameworkCore;
using MiniErp.Api.Data;
using MiniErp.Api.Domain;
using MiniErp.Api.Domain.Enums;
using MiniErp.Api.Infrastructure.Accounting;
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
                Plan = "DEV",
                TaxRegistrationNo = "123456789",
                Address = "Cairo, Egypt"
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

        var hasAnyUnitMeasures = await db.UnitMeasures.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId, cancellationToken);
        if (!hasAnyUnitMeasures)
        {
            var now = DateTimeOffset.UtcNow;
            db.UnitMeasures.AddRange(
                new UnitMeasure { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Unit", Capacity = 1m, IsActive = true, CreatedAt = now },
                new UnitMeasure { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Box", Capacity = 1m, IsActive = true, CreatedAt = now },
                new UnitMeasure { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Pack", Capacity = 1m, IsActive = true, CreatedAt = now },
                new UnitMeasure { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Dozen", Capacity = 12m, IsActive = true, CreatedAt = now },
                new UnitMeasure { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Kg", Capacity = 1m, IsActive = true, CreatedAt = now },
                new UnitMeasure { Id = Guid.NewGuid(), TenantId = tenantId, Name = "g", Capacity = 0.001m, IsActive = true, CreatedAt = now },
                new UnitMeasure { Id = Guid.NewGuid(), TenantId = tenantId, Name = "L", Capacity = 1m, IsActive = true, CreatedAt = now },
                new UnitMeasure { Id = Guid.NewGuid(), TenantId = tenantId, Name = "ml", Capacity = 0.001m, IsActive = true, CreatedAt = now });
        }

        var vat14Exists = await db.TaxRates.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId && x.Id == vat14Id, cancellationToken);
        if (!vat14Exists)
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
            var productsMissingTax = await db.Products.IgnoreQueryFilters()
                .Where(x => x.TenantId == tenantId && x.TaxRateId == null)
                .ToListAsync(cancellationToken);
            foreach (var p in productsMissingTax)
            {
                p.TaxRateId = vat14Id;
            }

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

        var seedTaxTypes = new (Guid? Id, string MainCode, string SubCode, string TaxType, string Description)[]
        {
            (null, "T_1", "V001", "VAT", "تصدير للخارج"),
            (null, "T_1", "V002", "VAT", "تصدير لمناطق حرة وأخرى"),
            (null, "T_1", "V003", "VAT", "سلعة أو خدمة معفاة"),
            (null, "T_1", "V004", "VAT", "سلعة أو خدمة غير خاضعة"),
            (null, "T_1", "V005", "VAT", "بيان"),
            (null, "T_1", "V006", "VAT", "إعفاءات دفاع وأمن قومي"),
            (null, "T_1", "V007", "VAT", "إعفاءات اتفاقيات"),
            (null, "T_1", "V008", "VAT", "إعفاءات أخرى"),
            (salesTaxTypeId, "T_1", "V009", "VAT", "سلع عامة"),
            (null, "T_1", "V010", "VAT", "سلع أخرى"),

            (null, "T_2", "Tb101", "ضريبة جدول", "ضريبة الجدول - نسبة"),
            (null, "T_3", "Tb102", "ضريبة جدول", "ضريبة الجدول - نوعية"),

            (null, "T_4", "W001", "الخصم تحت حساب ضريبة", "المقاولات"),
            (null, "T_4", "W002", "الخصم تحت حساب ضريبة", "التوريدات"),
            (null, "T_4", "W003", "الخصم تحت حساب ضريبة", "المشتريات"),
            (null, "T_4", "W004", "الخصم تحت حساب ضريبة", "الخدمات"),
            (null, "T_4", "W005", "الخصم تحت حساب ضريبة", "الجمعيات التعاونية للنقل"),
            (null, "T_4", "W006", "الخصم تحت حساب ضريبة", "العمولات والسمسرة"),
            (null, "T_4", "W007", "الخصم تحت حساب ضريبة", "شركات الدخل والائتمان"),
            (null, "T_4", "W008", "الخصم تحت حساب ضريبة", "شركات البترول والاتصالات"),
            (null, "T_4", "W009", "الخصم تحت حساب ضريبة", "المدفوعات"),
            (null, "T_4", "W010", "الخصم تحت حساب ضريبة", "أتعاب مهنية"),
            (null, "T_4", "W011", "الخصم تحت حساب ضريبة", "عمولة وسمسرة"),
            (null, "T_4", "W012", "الخصم تحت حساب ضريبة", "تحصيل المستشفيات من الأطباء"),
            (null, "T_4", "W013", "الخصم تحت حساب ضريبة", "إتاوات"),
            (null, "T_4", "W014", "الخصم تحت حساب ضريبة", "تخليص جمركي"),
            (null, "T_4", "W015", "الخصم تحت حساب ضريبة", "إلغاء"),
            (null, "T_4", "W016", "الخصم تحت حساب ضريبة", "دفعات مقدمة"),

            (null, "T_5", "ST01", "ضريبة الدمغة", "ضريبة الدمغة - نسبية"),
            (null, "T_6", "ST02", "ضريبة الدمغة", "ضريبة الدمغة - قطعية"),

            (null, "T_7", "Ent01", "ضريبة الملاهي", "ضريبة الملاهي - نسبية"),
            (null, "T_7", "Ent02", "ضريبة الملاهي", "ضريبة الملاهي - قطعية"),

            (null, "T_8", "RD01", "رسم تنمية الموارد", "رسم تنمية الموارد - نسبية"),
            (null, "T_8", "RD02", "رسم تنمية الموارد", "رسم تنمية الموارد - قطعية"),

            (null, "T_9", "RD01", "رسم خدمة", "رسم خدمة - نسبية"),
            (null, "T_9", "RD02", "رسم خدمة", "رسم خدمة - قطعية"),

            (null, "T_10", "Mn01", "رسم المحليات", "رسم المحليات - نسبية"),
            (null, "T_10", "Mn02", "رسم المحليات", "رسم المحليات - قطعية"),

            (null, "T_11", "MI01", "رسم التأمين الصحي", "رسم التأمين الصحي - نسبية"),
            (null, "T_11", "MI02", "رسم التأمين الصحي", "رسم التأمين الصحي - قطعية"),

            (null, "T_12", "OF01", "رسوم أخرى", "رسوم أخرى - نسبية"),
            (null, "T_12", "OF02", "رسوم أخرى", "رسوم أخرى - قطعية"),

            (null, "T_13", "ST03", "ضريبة الدمغة", "ضريبة الدمغة نسبية (غير ضريبي)"),
            (null, "T_14", "ST04", "ضريبة الدمغة", "ضريبة الدمغة قطعية (غير ضريبي)"),

            (null, "T_15", "Ent03", "ضريبة الملاهي", "ضريبة الملاهي - نسبية"),
            (null, "T_15", "Ent04", "ضريبة الملاهي", "ضريبة الملاهي - قطعية"),

            (null, "T_16", "RD03", "رسم تنمية الموارد", "رسم تنمية الموارد - نسبية"),
            (null, "T_16", "RD04", "رسم تنمية الموارد", "رسم تنمية الموارد - قطعية"),

            (null, "T_17", "SC03", "رسم خدمة", "رسم خدمة - نسبية"),
            (null, "T_17", "SC04", "رسم خدمة", "رسم خدمة - قطعية"),

            (null, "T_18", "Mn03", "رسم المحليات", "رسم المحليات - نسبية"),
            (null, "T_18", "Mn04", "رسم المحليات", "رسم المحليات - قطعية"),

            (null, "T_19", "MI03", "رسم التأمين الصحي", "رسم التأمين الصحي - نسبية"),
            (null, "T_19", "MI04", "رسم التأمين الصحي", "رسم التأمين الصحي - قطعية"),

            (null, "T_20", "OF03", "رسوم أخرى", "رسوم أخرى - نسبية"),
            (null, "T_20", "OF04", "رسوم أخرى", "رسوم أخرى - قطعية")
        };

        var existingTaxKeys = await db.SalesTaxTypes.IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId)
            .Select(x => new { x.MainCode, x.SubCode })
            .ToListAsync(cancellationToken);

        var existingTaxKeySet = new HashSet<string>(existingTaxKeys.Select(x => x.MainCode.Trim().ToUpperInvariant() + "|" + x.SubCode.Trim().ToUpperInvariant()));
        foreach (var t in seedTaxTypes)
        {
            var key = t.MainCode.Trim().ToUpperInvariant() + "|" + t.SubCode.Trim().ToUpperInvariant();
            if (existingTaxKeySet.Contains(key))
            {
                continue;
            }

            db.SalesTaxTypes.Add(new SalesTaxType
            {
                Id = t.Id ?? Guid.NewGuid(),
                TenantId = tenantId,
                MainCode = t.MainCode.Trim(),
                SubCode = t.SubCode.Trim(),
                TaxType = t.TaxType.Trim(),
                Description = t.Description.Trim(),
                Percent = 0m,
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
                TaxRegistrationNo = "SUP-123",
                Address = "Supplier Address",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        var hasAnyCustomers = await db.Customers.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId, cancellationToken);
        if (!hasAnyCustomers)
        {
            db.Customers.Add(new Customer
            {
                Id = Guid.Parse("66666666-6666-6666-6666-666666666666"),
                TenantId = tenantId,
                Name = "Cash Customer",
                Phone = "01000000001",
                TaxRegistrationNo = "CUST-123",
                Address = "Customer Address",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        var hasAnyChartAccounts = await db.ChartAccounts.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId, cancellationToken);
        if (!hasAnyChartAccounts)
        {
            var now = DateTimeOffset.UtcNow;
            db.ChartAccounts.AddRange(
                new ChartAccount { Id = Guid.NewGuid(), TenantId = tenantId, Code = AccountingEngine.Codes.Cash, Name = "Cash", AccountType = AccountType.Asset, ParentAccountId = null, IsPosting = true, IsActive = true, CreatedAt = now },
                new ChartAccount { Id = Guid.NewGuid(), TenantId = tenantId, Code = AccountingEngine.Codes.Bank, Name = "Bank", AccountType = AccountType.Asset, ParentAccountId = null, IsPosting = true, IsActive = true, CreatedAt = now },
                new ChartAccount { Id = Guid.NewGuid(), TenantId = tenantId, Code = AccountingEngine.Codes.VisaClearing, Name = "Visa Clearing", AccountType = AccountType.Asset, ParentAccountId = null, IsPosting = true, IsActive = true, CreatedAt = now },
                new ChartAccount { Id = Guid.NewGuid(), TenantId = tenantId, Code = AccountingEngine.Codes.Inventory, Name = "Inventory", AccountType = AccountType.Asset, ParentAccountId = null, IsPosting = true, IsActive = true, CreatedAt = now },
                new ChartAccount { Id = Guid.NewGuid(), TenantId = tenantId, Code = AccountingEngine.Codes.AccountsReceivable, Name = "Accounts Receivable", AccountType = AccountType.Asset, ParentAccountId = null, IsPosting = true, IsActive = true, CreatedAt = now },
                new ChartAccount { Id = Guid.NewGuid(), TenantId = tenantId, Code = AccountingEngine.Codes.InputVat, Name = "Input VAT", AccountType = AccountType.Asset, ParentAccountId = null, IsPosting = true, IsActive = true, CreatedAt = now },
                new ChartAccount { Id = Guid.NewGuid(), TenantId = tenantId, Code = AccountingEngine.Codes.AccountsPayable, Name = "Accounts Payable", AccountType = AccountType.Liability, ParentAccountId = null, IsPosting = true, IsActive = true, CreatedAt = now },
                new ChartAccount { Id = Guid.NewGuid(), TenantId = tenantId, Code = AccountingEngine.Codes.OutputVat, Name = "Output VAT", AccountType = AccountType.Liability, ParentAccountId = null, IsPosting = true, IsActive = true, CreatedAt = now },
                new ChartAccount { Id = Guid.NewGuid(), TenantId = tenantId, Code = AccountingEngine.Codes.SalesRevenue, Name = "Sales Revenue", AccountType = AccountType.Revenue, ParentAccountId = null, IsPosting = true, IsActive = true, CreatedAt = now },
                new ChartAccount { Id = Guid.NewGuid(), TenantId = tenantId, Code = AccountingEngine.Codes.SalesReturns, Name = "Sales Returns", AccountType = AccountType.Expense, ParentAccountId = null, IsPosting = true, IsActive = true, CreatedAt = now }
            );
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
