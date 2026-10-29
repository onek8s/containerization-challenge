using Microsoft.EntityFrameworkCore;
using ProductReturns.Core;

namespace ProductReturns.Data
{
    public class ReturnsDbContext : DbContext
    {
        public ReturnsDbContext(DbContextOptions<ReturnsDbContext> options)
            : base(options)
        {
        }

        public DbSet<ReturnRequest> Returns { get; set; }
        public DbSet<ReturnLine> ReturnLines { get; set; }
        public DbSet<ReturnNote> ReturnNotes { get; set; }
        public DbSet<ScannerDevice> Scanners { get; set; }
        public DbSet<SalesOrder> Orders { get; set; }
        public DbSet<SalesOrderLine> OrderLines { get; set; }
        public DbSet<CatalogItem> Catalog { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // The development database is SQLite, which has no schemas.
            var rtn = Database.IsSqlite() ? null : "rtn";
            var erp = Database.IsSqlite() ? null : "erp";

            modelBuilder.Entity<ReturnRequest>(e =>
            {
                e.ToTable("ReturnRequest", rtn);
                e.HasKey(x => x.Id);
                e.Property(x => x.RmaNumber).HasMaxLength(20);
                e.HasIndex(x => x.RmaNumber).IsUnique();
                e.HasIndex(x => new { x.Status, x.CreatedUtc });
                e.HasIndex(x => x.OrderNumber);
                e.Property(x => x.OrderNumber).HasMaxLength(20).IsRequired();
                e.Property(x => x.CustomerName).HasMaxLength(200);
                e.Property(x => x.CustomerEmail).HasMaxLength(254);
                e.Property(x => x.Currency).HasMaxLength(3).IsRequired();
                e.Property(x => x.CreatedBy).HasMaxLength(100);
                e.Property(x => x.DecidedBy).HasMaxLength(100);
                e.Property(x => x.InspectedBy).HasMaxLength(100);
                e.Property(x => x.RejectReason).HasMaxLength(500);
                e.Property(x => x.TrackingNumber).HasMaxLength(40);
                e.Property(x => x.LabelPath).HasMaxLength(400);
                e.Property(x => x.LabelError).HasMaxLength(1000);
                e.Property(x => x.RefundBatch).HasMaxLength(20);
                e.Property(x => x.RefundAmount).HasColumnType("decimal(12,2)");
                e.Property(x => x.RestockingFee).HasColumnType("decimal(12,2)");
                e.Property(x => x.LabelFee).HasColumnType("decimal(12,2)");
                if (Database.IsSqlite())
                {
                    e.Ignore(x => x.RowVersion);
                }
                else
                {
                    e.Property(x => x.RowVersion).IsRowVersion();
                }

                e.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.ReturnRequestId);
                e.HasMany(x => x.Notes).WithOne().HasForeignKey(n => n.ReturnRequestId);
            });

            modelBuilder.Entity<ReturnLine>(e =>
            {
                e.ToTable("ReturnLine", rtn);
                e.Property(x => x.Sku).HasMaxLength(40).IsRequired();
                e.Property(x => x.Description).HasMaxLength(200);
                e.Property(x => x.Category).HasMaxLength(40);
                e.Property(x => x.ReasonCode).HasMaxLength(30).IsRequired();
                e.Property(x => x.CustomerComment).HasMaxLength(1000);
                e.Property(x => x.UnitPrice).HasColumnType("decimal(12,2)");
                e.Property(x => x.RefundAmount).HasColumnType("decimal(12,2)");
            });

            modelBuilder.Entity<ReturnNote>(e =>
            {
                e.ToTable("ReturnNote", rtn);
                e.Property(x => x.Author).HasMaxLength(100).IsRequired();
                e.Property(x => x.Text).HasMaxLength(2000).IsRequired();
            });

            modelBuilder.Entity<ScannerDevice>(e =>
            {
                e.ToTable("ScannerDevice", rtn);
                e.Property(x => x.Name).HasMaxLength(50).IsRequired();
                e.HasIndex(x => x.Name).IsUnique();
                e.Property(x => x.Location).HasMaxLength(100);
                e.Property(x => x.ApiKeyHash).HasMaxLength(64).IsRequired();
            });

            // ERP replica, loaded every night by the ERP export (read only for this application).
            modelBuilder.Entity<SalesOrder>(e =>
            {
                e.ToTable("SalesOrder", erp);
                e.HasKey(x => x.OrderNumber);
                e.Property(x => x.OrderNumber).HasMaxLength(20);
                e.Property(x => x.Currency).HasMaxLength(3);
                e.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.OrderNumber);
            });

            modelBuilder.Entity<SalesOrderLine>(e =>
            {
                e.ToTable("SalesOrderLine", erp);
                e.HasKey(x => new { x.OrderNumber, x.LineNumber });
                e.Property(x => x.UnitPrice).HasColumnType("decimal(12,2)");
            });

            modelBuilder.Entity<CatalogItem>(e =>
            {
                e.ToTable("Item", erp);
                e.HasKey(x => x.Sku);
                e.Property(x => x.Sku).HasMaxLength(40);
                e.Property(x => x.WeightKg).HasColumnType("decimal(9,3)");
            });
        }
    }
}
