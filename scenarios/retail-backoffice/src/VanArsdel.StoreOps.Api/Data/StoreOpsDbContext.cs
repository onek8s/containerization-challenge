using Microsoft.EntityFrameworkCore;
using VanArsdel.BuildingBlocks.Outbox;
using VanArsdel.StoreOps.Api.Domain;

namespace VanArsdel.StoreOps.Api.Data
{
    public class StoreOpsDbContext : DbContext, IOutboxContext
    {
        public StoreOpsDbContext(DbContextOptions<StoreOpsDbContext> options) : base(options)
        {
        }

        public DbSet<Store> Stores { get; set; }
        public DbSet<PickTask> PickTasks { get; set; }
        public DbSet<Collection> Collections { get; set; }
        public DbSet<DailyTakings> Takings { get; set; }
        public DbSet<CashUp> CashUps { get; set; }
        public DbSet<Footfall> Footfall { get; set; }
        public DbSet<OutboxMessage> OutboxMessages { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Store>(b =>
            {
                b.ToTable("Store");
                b.HasKey(x => x.Code);
                b.Property(x => x.Code).HasMaxLength(10);
                b.Property(x => x.Name).HasMaxLength(100).IsRequired();
                b.Property(x => x.Type).HasMaxLength(10).IsRequired();
                b.Property(x => x.Region).HasMaxLength(30);
                b.Property(x => x.Country).HasMaxLength(2).IsRequired();
                b.Property(x => x.City).HasMaxLength(60);
                b.Property(x => x.Street).HasMaxLength(100);
                b.Property(x => x.PostalCode).HasMaxLength(10);
                b.Property(x => x.Currency).HasMaxLength(3);
                b.Property(x => x.TimeZone).HasMaxLength(40);
                b.Property(x => x.Phone).HasMaxLength(30);
                b.Property(x => x.ManagerEmail).HasMaxLength(200);
                b.Property(x => x.OpeningHours).HasMaxLength(500);
            });
            modelBuilder.Entity<PickTask>(b =>
            {
                b.ToTable("PickTask");
                b.HasIndex(x => new { x.StoreCode, x.Status });
                b.HasIndex(x => x.ShipmentId).IsUnique();
                b.Property(x => x.StoreCode).HasMaxLength(10);
                b.Property(x => x.OrderNo).HasMaxLength(30);
                b.Property(x => x.DeliveryMethod).HasMaxLength(20);
                b.Property(x => x.CustomerName).HasMaxLength(200);
                b.Property(x => x.Status).HasMaxLength(20);
                b.Property(x => x.AssignedTo).HasMaxLength(100);
                b.Property(x => x.WeightKg).HasColumnType("decimal(9,3)");
                b.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.PickTaskId);
            });
            modelBuilder.Entity<PickTaskLine>(b =>
            {
                b.ToTable("PickTaskLine");
                b.Property(x => x.Sku).HasMaxLength(40);
            });
            modelBuilder.Entity<Collection>(b =>
            {
                b.ToTable("Collection");
                b.HasIndex(x => new { x.StoreCode, x.Status });
                b.HasIndex(x => x.OrderNo);
                b.Property(x => x.StoreCode).HasMaxLength(10);
                b.Property(x => x.OrderNo).HasMaxLength(30);
                b.Property(x => x.CustomerEmail).HasMaxLength(200);
                b.Property(x => x.CustomerPhone).HasMaxLength(30);
                b.Property(x => x.Status).HasMaxLength(20);
                b.Property(x => x.HandedOverBy).HasMaxLength(100);
            });
            modelBuilder.Entity<DailyTakings>(b =>
            {
                b.ToTable("DailyTakings");
                b.HasIndex(x => new { x.StoreCode, x.BusinessDate }).IsUnique();
                b.Property(x => x.StoreCode).HasMaxLength(10);
                b.Property(x => x.LastFile).HasMaxLength(100);
                foreach (var p in new[] { nameof(DailyTakings.NetSales), nameof(DailyTakings.ReturnsValue), nameof(DailyTakings.CashTotal), nameof(DailyTakings.CardTotal), nameof(DailyTakings.OtherTotal) })
                {
                    b.Property(p).HasColumnType("decimal(18,2)");
                }
            });
            modelBuilder.Entity<CashUp>(b =>
            {
                b.ToTable("CashUp");
                b.HasIndex(x => new { x.StoreCode, x.BusinessDate });
                b.Property(x => x.StoreCode).HasMaxLength(10);
                b.Property(x => x.CountedBy).HasMaxLength(100);
                b.Property(x => x.Notes).HasMaxLength(500);
                b.Property(x => x.ExpectedCash).HasColumnType("decimal(18,2)");
                b.Property(x => x.CountedCash).HasColumnType("decimal(18,2)");
                b.Property(x => x.Difference).HasColumnType("decimal(18,2)");
            });
            modelBuilder.Entity<Footfall>(b =>
            {
                b.ToTable("Footfall");
                b.HasIndex(x => new { x.StoreCode, x.Date, x.Hour }).IsUnique();
                b.Property(x => x.StoreCode).HasMaxLength(10);
            });
            modelBuilder.AddOutbox();
        }
    }
}
