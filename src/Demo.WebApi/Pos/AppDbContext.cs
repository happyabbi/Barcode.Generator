using Microsoft.EntityFrameworkCore;

namespace Demo.WebApi.Pos;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<BarcodeEntry> Barcodes => Set<BarcodeEntry>();
    public DbSet<InventoryLevel> InventoryLevels => Set<InventoryLevel>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();
    public DbSet<SalesOrder> SalesOrders => Set<SalesOrder>();
    public DbSet<SalesOrderItem> SalesOrderItems => Set<SalesOrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>()
            .HasIndex(p => p.Sku)
            .IsUnique();

        modelBuilder.Entity<BarcodeEntry>()
            .HasIndex(b => new { b.Format, b.CodeValue })
            .IsUnique();

        modelBuilder.Entity<InventoryLevel>()
            .HasIndex(i => i.ProductId)
            .IsUnique();

        modelBuilder.Entity<Product>()
            .Property(p => p.Price)
            .HasPrecision(12, 2);

        modelBuilder.Entity<Product>()
            .Property(p => p.Cost)
            .HasPrecision(12, 2);

        modelBuilder.Entity<SalesOrder>()
            .HasIndex(o => o.OrderNo)
            .IsUnique();

        modelBuilder.Entity<SalesOrder>()
            .Property(o => o.Subtotal)
            .HasPrecision(12, 2);

        modelBuilder.Entity<SalesOrder>()
            .Property(o => o.Discount)
            .HasPrecision(12, 2);

        modelBuilder.Entity<SalesOrder>()
            .Property(o => o.Total)
            .HasPrecision(12, 2);

        modelBuilder.Entity<SalesOrder>()
            .Property(o => o.PaidAmount)
            .HasPrecision(12, 2);

        modelBuilder.Entity<SalesOrder>()
            .Property(o => o.ChangeAmount)
            .HasPrecision(12, 2);

        modelBuilder.Entity<SalesOrderItem>()
            .Property(i => i.UnitPrice)
            .HasPrecision(12, 2);

        modelBuilder.Entity<SalesOrderItem>()
            .Property(i => i.LineTotal)
            .HasPrecision(12, 2);
    }
}
