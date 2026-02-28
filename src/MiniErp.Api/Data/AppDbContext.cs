using Microsoft.EntityFrameworkCore;
using MiniErp.Api.Domain;
using MiniErp.Api.Domain.Enums;
using MiniErp.Api.Infrastructure.Tenancy;

namespace MiniErp.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options, ITenantProvider tenantProvider) : DbContext(options)
{
    public Guid TenantId { get; } = tenantProvider.TenantId;

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<User> Users => Set<User>();

    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductUnit> ProductUnits => Set<ProductUnit>();
    public DbSet<Barcode> Barcodes => Set<Barcode>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<TaxRate> TaxRates => Set<TaxRate>();
    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<Return> Returns => Set<Return>();
    public DbSet<ReturnItem> ReturnItems => Set<ReturnItem>();

    public DbSet<StockLedger> StockLedgers => Set<StockLedger>();
    public DbSet<StockBalance> StockBalances => Set<StockBalance>();

    public DbSet<CashTxn> CashTxns => Set<CashTxn>();
    public DbSet<ChartAccount> ChartAccounts => Set<ChartAccount>();
    public DbSet<PrintJob> PrintJobs => Set<PrintJob>();

    public DbSet<SalesTaxType> SalesTaxTypes => Set<SalesTaxType>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<PurchaseItem> PurchaseItems => Set<PurchaseItem>();

    public DbSet<IdempotencyKey> IdempotencyKeys => Set<IdempotencyKey>();
    public DbSet<Counter> Counters => Set<Counter>();
    public DbSet<InventoryAdjustment> InventoryAdjustments => Set<InventoryAdjustment>();
    public DbSet<InventoryAdjustmentLine> InventoryAdjustmentLines => Set<InventoryAdjustmentLine>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserPermission> UserPermissions => Set<UserPermission>();
    public DbSet<UserPermissionProfile> UserPermissionProfiles => Set<UserPermissionProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>(b =>
        {
            b.ToTable("Tenants");
            b.HasKey(x => x.Id);
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.Plan).HasMaxLength(50);
        });

        modelBuilder.Entity<Branch>(b =>
        {
            b.ToTable("Branches");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.TenantId, x.Name });
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.HasQueryFilter(x => x.TenantId == TenantId);
        });

        modelBuilder.Entity<Device>(b =>
        {
            b.ToTable("Devices");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.TenantId, x.BranchId });
            b.HasIndex(x => new { x.TenantId, x.DeviceKey }).IsUnique();
            b.Property(x => x.DeviceKey).HasMaxLength(200).IsRequired();
            b.HasQueryFilter(x => x.TenantId == TenantId);
        });

        modelBuilder.Entity<User>(b =>
        {
            b.ToTable("Users");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.PinHash).HasMaxLength(300).IsRequired();
            b.Property(x => x.Role).HasConversion<int>();
            b.HasQueryFilter(x => x.TenantId == TenantId);
        });

        modelBuilder.Entity<Product>(b =>
        {
            b.ToTable("Products");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.TenantId, x.Name });
            b.HasIndex(x => new { x.TenantId, x.CategoryId });
            b.HasIndex(x => new { x.TenantId, x.TaxRateId });
            b.HasIndex(x => new { x.TenantId, x.SalesTaxTypeId });
            b.Property(x => x.Name).HasMaxLength(300).IsRequired();
            b.Property(x => x.Sku).HasMaxLength(100);
            b.Property(x => x.Cost).HasPrecision(18, 3);
            b.Property(x => x.Price).HasPrecision(18, 3);
            b.Property(x => x.ReorderLevel).HasPrecision(18, 3);
            b.HasQueryFilter(x => x.TenantId == TenantId);
        });

        modelBuilder.Entity<ProductUnit>(b =>
        {
            b.ToTable("ProductUnits");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.TenantId, x.ProductId });
            b.HasIndex(x => new { x.TenantId, x.ProductId, x.Name }).IsUnique();
            b.Property(x => x.Name).HasMaxLength(50).IsRequired();
            b.Property(x => x.Factor).HasPrecision(18, 6);
            b.HasQueryFilter(x => x.TenantId == TenantId);
        });

        modelBuilder.Entity<Category>(b =>
        {
            b.ToTable("Categories");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.HasQueryFilter(x => x.TenantId == TenantId);
        });

        modelBuilder.Entity<TaxRate>(b =>
        {
            b.ToTable("TaxRates");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
            b.Property(x => x.Name).HasMaxLength(100).IsRequired();
            b.Property(x => x.Percent).HasPrecision(9, 6);
            b.HasQueryFilter(x => x.TenantId == TenantId);
        });

        modelBuilder.Entity<Customer>(b =>
        {
            b.ToTable("Customers");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.TenantId, x.Name });
            b.Property(x => x.Name).HasMaxLength(300).IsRequired();
            b.Property(x => x.Phone).HasMaxLength(50);
            b.HasQueryFilter(x => x.TenantId == TenantId);
        });

        modelBuilder.Entity<Barcode>(b =>
        {
            b.ToTable("Barcodes");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.ProductId });
            b.Property(x => x.Code).HasMaxLength(100).IsRequired();
            b.HasQueryFilter(x => x.TenantId == TenantId);
        });

        modelBuilder.Entity<Sale>(b =>
        {
            b.ToTable("Sales");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.TenantId, x.BranchId, x.At });
            b.HasIndex(x => new { x.TenantId, x.Number }).IsUnique();
            b.Property(x => x.Number).HasMaxLength(50).IsRequired();
            b.Property(x => x.CustomerName).HasMaxLength(300);
            b.Property(x => x.Total).HasPrecision(18, 3);
            b.Property(x => x.TaxTotal).HasPrecision(18, 3);
            b.Property(x => x.Status).HasConversion<int>();
            b.HasQueryFilter(x => x.TenantId == TenantId);
        });

        modelBuilder.Entity<SaleItem>(b =>
        {
            b.ToTable("SaleItems");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.TenantId, x.SaleId });
            b.Property(x => x.Qty).HasPrecision(18, 3);
            b.Property(x => x.QtyBase).HasPrecision(18, 3);
            b.Property(x => x.UnitName).HasMaxLength(50);
            b.Property(x => x.UnitFactor).HasPrecision(18, 6);
            b.Property(x => x.UnitPrice).HasPrecision(18, 3);
            b.Property(x => x.Discount).HasPrecision(18, 3);
            b.Property(x => x.LineTotal).HasPrecision(18, 3);
            b.Property(x => x.UnitCost).HasPrecision(18, 3);
            b.Property(x => x.TaxPercent).HasPrecision(9, 6);
            b.Property(x => x.TaxAmount).HasPrecision(18, 3);
            b.HasQueryFilter(x => x.TenantId == TenantId);
        });

        modelBuilder.Entity<Payment>(b =>
        {
            b.ToTable("Payments");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.TenantId, x.SaleId });
            b.Property(x => x.Method).HasMaxLength(50).IsRequired();
            b.Property(x => x.Amount).HasPrecision(18, 3);
            b.Property(x => x.ReferenceNo).HasMaxLength(100);
            b.Property(x => x.Note).HasMaxLength(500);
            b.HasQueryFilter(x => x.TenantId == TenantId);
        });

        modelBuilder.Entity<Return>(b =>
        {
            b.ToTable("Returns");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.TenantId, x.BranchId, x.At });
            b.HasIndex(x => new { x.TenantId, x.Number }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.OrigSaleId });
            b.Property(x => x.Number).HasMaxLength(50).IsRequired();
            b.Property(x => x.Total).HasPrecision(18, 3);
            b.Property(x => x.RefundMethod).HasMaxLength(50).IsRequired();
            b.HasQueryFilter(x => x.TenantId == TenantId);
        });

        modelBuilder.Entity<ReturnItem>(b =>
        {
            b.ToTable("ReturnItems");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.TenantId, x.ReturnId });
            b.Property(x => x.Qty).HasPrecision(18, 3);
            b.Property(x => x.QtyBase).HasPrecision(18, 3);
            b.Property(x => x.UnitName).HasMaxLength(50);
            b.Property(x => x.UnitFactor).HasPrecision(18, 6);
            b.Property(x => x.UnitPrice).HasPrecision(18, 3);
            b.Property(x => x.Discount).HasPrecision(18, 3);
            b.Property(x => x.LineTotal).HasPrecision(18, 3);
            b.Property(x => x.TaxPercent).HasPrecision(9, 6);
            b.Property(x => x.TaxAmount).HasPrecision(18, 3);
            b.HasQueryFilter(x => x.TenantId == TenantId);
        });

        modelBuilder.Entity<StockLedger>(b =>
        {
            b.ToTable("StockLedger");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.TenantId, x.BranchId, x.ProductId, x.At });
            b.Property(x => x.QtyDelta).HasPrecision(18, 3);
            b.Property(x => x.Reason).HasConversion<int>();
            b.Property(x => x.RefType).HasMaxLength(50).IsRequired();
            b.HasQueryFilter(x => x.TenantId == TenantId);
        });

        modelBuilder.Entity<StockBalance>(b =>
        {
            b.ToTable("StockBalance");
            b.HasKey(x => new { x.TenantId, x.BranchId, x.ProductId });
            b.Property(x => x.Qty).HasPrecision(18, 3);
            b.HasIndex(x => new { x.TenantId, x.BranchId, x.ProductId }).IsUnique();
            b.HasQueryFilter(x => x.TenantId == TenantId);
        });

        modelBuilder.Entity<CashTxn>(b =>
        {
            b.ToTable("CashTxns");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.TenantId, x.BranchId, x.At });
            b.Property(x => x.Type).HasConversion<int>();
            b.Property(x => x.Amount).HasPrecision(18, 3);
            b.Property(x => x.RefType).HasMaxLength(50).IsRequired();
            b.HasQueryFilter(x => x.TenantId == TenantId);
        });

        modelBuilder.Entity<ChartAccount>(b =>
        {
            b.ToTable("ChartAccounts");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.ParentAccountId });
            b.Property(x => x.Code).HasMaxLength(50).IsRequired();
            b.Property(x => x.Name).HasMaxLength(300).IsRequired();
            b.Property(x => x.AccountType).HasConversion<int>();
            b.HasQueryFilter(x => x.TenantId == TenantId);
        });

        modelBuilder.Entity<PrintJob>(b =>
        {
            b.ToTable("PrintJobs");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.TenantId, x.BranchId, x.Status, x.NextRetryAt });
            b.Property(x => x.RefType).HasMaxLength(50).IsRequired();
            b.Property(x => x.Status).HasConversion<int>();
            b.Property(x => x.PayloadJson).IsRequired();
            b.Property(x => x.LastError).HasMaxLength(2000);
            b.HasQueryFilter(x => x.TenantId == TenantId);
        });

        modelBuilder.Entity<IdempotencyKey>(b =>
        {
            b.ToTable("IdempotencyKeys");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.TenantId, x.DeviceId, x.Key }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.DeviceId, x.Endpoint });
            b.HasIndex(x => new { x.TenantId, x.Status, x.LockedUntil });
            b.Property(x => x.Key).HasMaxLength(100).IsRequired();
            b.Property(x => x.Endpoint).HasMaxLength(200).IsRequired();
            b.Property(x => x.RequestHash).HasMaxLength(128).IsRequired();
            b.Property(x => x.Status).HasConversion<int>();
            b.Property(x => x.LastError).HasMaxLength(2000);
            b.Property(x => x.ResponseBody);
            b.HasQueryFilter(x => x.TenantId == TenantId);
        });

        modelBuilder.Entity<Counter>(b =>
        {
            b.ToTable("Counters");
            b.HasKey(x => new { x.TenantId, x.Name });
            b.Property(x => x.Name).HasMaxLength(50).IsRequired();
            b.HasQueryFilter(x => x.TenantId == TenantId);
        });

        modelBuilder.Entity<InventoryAdjustment>(b =>
        {
            b.ToTable("InventoryAdjustments");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.TenantId, x.BranchId, x.At });
            b.HasIndex(x => new { x.TenantId, x.Number }).IsUnique();
            b.Property(x => x.Number).HasMaxLength(50).IsRequired();
            b.Property(x => x.Note).HasMaxLength(1000);
            b.HasQueryFilter(x => x.TenantId == TenantId);
        });

        modelBuilder.Entity<InventoryAdjustmentLine>(b =>
        {
            b.ToTable("InventoryAdjustmentLines");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.TenantId, x.AdjustmentId });
            b.Property(x => x.QtyDelta).HasPrecision(18, 3);
            b.HasQueryFilter(x => x.TenantId == TenantId);
        });

        modelBuilder.Entity<SalesTaxType>(b =>
        {
            b.ToTable("SalesTaxTypes");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.TenantId, x.MainCode, x.SubCode }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.TaxType });
            b.Property(x => x.MainCode).HasMaxLength(20).IsRequired();
            b.Property(x => x.SubCode).HasMaxLength(20).IsRequired();
            b.Property(x => x.TaxType).HasMaxLength(50).IsRequired();
            b.Property(x => x.Description).HasMaxLength(500).IsRequired();
            b.HasQueryFilter(x => x.TenantId == TenantId);
        });

        modelBuilder.Entity<Supplier>(b =>
        {
            b.ToTable("Suppliers");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.TenantId, x.Name });
            b.Property(x => x.Name).HasMaxLength(300).IsRequired();
            b.Property(x => x.Phone).HasMaxLength(50);
            b.HasQueryFilter(x => x.TenantId == TenantId);
        });

        modelBuilder.Entity<Purchase>(b =>
        {
            b.ToTable("Purchases");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.TenantId, x.BranchId, x.At });
            b.HasIndex(x => new { x.TenantId, x.Number }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.SupplierId });
            b.Property(x => x.Number).HasMaxLength(50).IsRequired();
            b.Property(x => x.SupplierName).HasMaxLength(300);
            b.Property(x => x.Total).HasPrecision(18, 3);
            b.Property(x => x.TaxTotal).HasPrecision(18, 3);
            b.Property(x => x.CashPaid).HasPrecision(18, 3);
            b.Property(x => x.Note).HasMaxLength(1000);
            b.HasQueryFilter(x => x.TenantId == TenantId);
        });

        modelBuilder.Entity<PurchaseItem>(b =>
        {
            b.ToTable("PurchaseItems");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.TenantId, x.PurchaseId });
            b.Property(x => x.Qty).HasPrecision(18, 3);
            b.Property(x => x.QtyBase).HasPrecision(18, 3);
            b.Property(x => x.UnitName).HasMaxLength(50);
            b.Property(x => x.UnitFactor).HasPrecision(18, 6);
            b.Property(x => x.UnitCost).HasPrecision(18, 3);
            b.Property(x => x.LineTotal).HasPrecision(18, 3);
            b.Property(x => x.TaxPercent).HasPrecision(9, 6);
            b.Property(x => x.TaxAmount).HasPrecision(18, 3);
            b.HasQueryFilter(x => x.TenantId == TenantId);
        });

        modelBuilder.Entity<RolePermission>(b =>
        {
            b.ToTable("RolePermissions");
            b.HasKey(x => new { x.TenantId, x.Role, x.PermissionKey });
            b.HasIndex(x => new { x.TenantId, x.Role });
            b.Property(x => x.Role).HasConversion<int>();
            b.Property(x => x.PermissionKey).HasMaxLength(100).IsRequired();
            b.HasQueryFilter(x => x.TenantId == TenantId);
        });

        modelBuilder.Entity<UserPermission>(b =>
        {
            b.ToTable("UserPermissions");
            b.HasKey(x => new { x.TenantId, x.UserId, x.PermissionKey });
            b.HasIndex(x => new { x.TenantId, x.UserId });
            b.Property(x => x.PermissionKey).HasMaxLength(100).IsRequired();
            b.HasQueryFilter(x => x.TenantId == TenantId);
        });

        modelBuilder.Entity<UserPermissionProfile>(b =>
        {
            b.ToTable("UserPermissionProfiles");
            b.HasKey(x => new { x.TenantId, x.UserId });
            b.HasIndex(x => new { x.TenantId, x.UserId });
            b.Property(x => x.UpdatedAt).IsRequired();
            b.HasQueryFilter(x => x.TenantId == TenantId);
        });
    }
}
