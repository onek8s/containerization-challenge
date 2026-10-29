using System.Data.Entity;
using System.Data.Entity.ModelConfiguration.Conventions;
using Microsoft.AspNet.Identity.EntityFramework;
using Wingtip.FieldSales.Data.Entities;

namespace Wingtip.FieldSales.Data
{
    /// <summary>
    /// One context for the application tables and the ASP.NET Identity tables. The schema is created by the
    /// scripts in the db folder; the context never creates or changes tables.
    /// </summary>
    public class FieldSalesDbContext : IdentityDbContext<ApplicationUser>
    {
        static FieldSalesDbContext()
        {
            Database.SetInitializer<FieldSalesDbContext>(null);
        }

        public FieldSalesDbContext() : base("name=FieldSales", throwIfV1Schema: false)
        {
        }

        public static FieldSalesDbContext Create()
        {
            return new FieldSalesDbContext();
        }

        public DbSet<CustomerEntity> Customers { get; set; }
        public DbSet<ProductEntity> Products { get; set; }
        public DbSet<PriceRuleEntity> PriceRules { get; set; }
        public DbSet<PromotionEntity> Promotions { get; set; }
        public DbSet<HolidayEntity> Holidays { get; set; }
        public DbSet<SalesOrderEntity> SalesOrders { get; set; }
        public DbSet<SalesOrderLineEntity> SalesOrderLines { get; set; }
        public DbSet<RefreshTokenEntity> RefreshTokens { get; set; }
        public DbSet<PriceListImportEntity> PriceListImports { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Conventions.Remove<PluralizingTableNameConvention>();

            modelBuilder.Entity<CustomerEntity>().ToTable("Customer").HasKey(c => c.CustomerId);
            modelBuilder.Entity<ProductEntity>().ToTable("Product").HasKey(p => p.ProductId);
            modelBuilder.Entity<ProductEntity>().Property(p => p.RowVer).IsRowVersion();
            modelBuilder.Entity<ProductEntity>().Property(p => p.ListPrice).HasPrecision(12, 2);
            modelBuilder.Entity<ProductEntity>().Property(p => p.VatRate).HasPrecision(5, 2);
            modelBuilder.Entity<PriceRuleEntity>().ToTable("PriceRule").HasKey(r => r.PriceRuleId);
            modelBuilder.Entity<PriceRuleEntity>().Property(r => r.DiscountPercent).HasPrecision(5, 2);
            modelBuilder.Entity<PromotionEntity>().ToTable("Promotion").HasKey(p => p.PromotionId);
            modelBuilder.Entity<PromotionEntity>().Property(p => p.PromoPrice).HasPrecision(12, 2);
            modelBuilder.Entity<HolidayEntity>().ToTable("Holiday").HasKey(h => h.Day);

            var order = modelBuilder.Entity<SalesOrderEntity>().ToTable("SalesOrder");
            order.HasKey(o => o.SalesOrderId);
            order.Property(o => o.NetTotal).HasPrecision(14, 2);
            order.Property(o => o.VatTotal).HasPrecision(14, 2);
            order.Property(o => o.ManualDiscountPercent).HasPrecision(5, 2);
            order.HasMany(o => o.Lines).WithRequired().HasForeignKey(l => l.SalesOrderId);
            order.HasRequired(o => o.Customer).WithMany().HasForeignKey(o => o.CustomerId).WillCascadeOnDelete(false);

            var line = modelBuilder.Entity<SalesOrderLineEntity>().ToTable("SalesOrderLine");
            line.HasKey(l => new { l.SalesOrderId, l.LineNo });
            line.Property(l => l.UnitPrice).HasPrecision(12, 2);
            line.Property(l => l.LineDiscountPercent).HasPrecision(5, 2);
            line.Property(l => l.VatRate).HasPrecision(5, 2);
            line.Property(l => l.LineTotal).HasPrecision(14, 2);

            modelBuilder.Entity<RefreshTokenEntity>().ToTable("RefreshToken").HasKey(t => t.TokenHash);
            modelBuilder.Entity<PriceListImportEntity>().ToTable("PriceListImport").HasKey(i => i.PriceListImportId);
        }
    }
}
