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
    public DbSet<Barcode> Barcodes => Set<Barcode>();

    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<Return> Returns => Set<Return>();
    public DbSet<ReturnItem> ReturnItems => Set<ReturnItem>();

    public DbSet<StockLedger> StockLedgers => Set<StockLedger>();
    public DbSet<StockBalance> StockBalances => Set<StockBalance>();

    public DbSet<CashTxn> CashTxns => Set<CashTxn>();
    public DbSet<PrintJob> PrintJobs => Set<PrintJob>();

    public DbSet<IdempotencyKey> IdempotencyKeys => Set<IdempotencyKey>();
    public DbSet<Counter> Counters => Set<Counter>();
    public DbSet<InventoryAdjustment> InventoryAdjustments => Set<InventoryAdjustment>();
    public DbSet<InventoryAdjustmentLine> InventoryAdjustmentLines => Set<InventoryAdjustmentLine>();

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
            b.Property(x => x.Name).HasMaxLength(300).IsRequired();
            b.Property(x => x.Sku).HasMaxLength(100);
            b.Property(x => x.Cost).HasPrecision(18, 3);
            b.Property(x => x.Price).HasPrecision(18, 3);
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
            b.Property(x => x.Total).HasPrecision(18, 3);
            b.Property(x => x.Status).HasConversion<int>();
            b.HasQueryFilter(x => x.TenantId == TenantId);
        });

        modelBuilder.Entity<SaleItem>(b =>
        {
            b.ToTable("SaleItems");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.TenantId, x.SaleId });
            b.Property(x => x.Qty).HasPrecision(18, 3);
            b.Property(x => x.UnitPrice).HasPrecision(18, 3);
            b.Property(x => x.Discount).HasPrecision(18, 3);
            b.Property(x => x.LineTotal).HasPrecision(18, 3);
            b.Property(x => x.UnitCost).HasPrecision(18, 3);
            b.HasQueryFilter(x => x.TenantId == TenantId);
        });

        modelBuilder.Entity<Payment>(b =>
        {
            b.ToTable("Payments");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.TenantId, x.SaleId });
            b.Property(x => x.Method).HasMaxLength(50).IsRequired();
            b.Property(x => x.Amount).HasPrecision(18, 3);
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
            b.Property(x => x.UnitPrice).HasPrecision(18, 3);
            b.Property(x => x.Discount).HasPrecision(18, 3);
            b.Property(x => x.LineTotal).HasPrecision(18, 3);
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
            b.Property(x => x.Key).HasMaxLength(100).IsRequired();
            b.Property(x => x.Endpoint).HasMaxLength(200).IsRequired();
            b.Property(x => x.RequestHash).HasMaxLength(128).IsRequired();
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
    }
}
