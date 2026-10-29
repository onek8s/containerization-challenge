using Microsoft.EntityFrameworkCore;
using VanArsdel.BuildingBlocks.Outbox;
using VanArsdel.Inventory.Api.Domain;

namespace VanArsdel.Inventory.Api.Data
{
    public class InventoryDbContext : DbContext, IOutboxContext
    {
        public InventoryDbContext(DbContextOptions<InventoryDbContext> options) : base(options)
        {
        }

        public DbSet<Location> Locations { get; set; }
        public DbSet<Item> Items { get; set; }
        public DbSet<StockLevel> StockLevels { get; set; }
        public DbSet<StockMovement> Movements { get; set; }
        public DbSet<Reservation> Reservations { get; set; }
        public DbSet<ProcessedMessage> ProcessedMessages { get; set; }
        public DbSet<ReplenishmentProposal> ReplenishmentProposals { get; set; }
        public DbSet<OutboxMessage> OutboxMessages { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Location>(b =>
            {
                b.ToTable("Location");
                b.HasKey(x => x.Code);
                b.Property(x => x.Code).HasMaxLength(10);
                b.Property(x => x.Name).HasMaxLength(100).IsRequired();
                b.Property(x => x.Type).HasMaxLength(10).IsRequired();
                b.Property(x => x.Country).HasMaxLength(2).IsRequired();
                b.Property(x => x.City).HasMaxLength(60);
            });

            modelBuilder.Entity<Item>(b =>
            {
                b.ToTable("Item");
                b.HasKey(x => x.Sku);
                b.Property(x => x.Sku).HasMaxLength(40);
                b.Property(x => x.StyleCode).HasMaxLength(20).IsRequired();
                b.HasIndex(x => x.StyleCode);
                b.Property(x => x.Ean).HasMaxLength(13);
                b.Property(x => x.CategoryCode).HasMaxLength(20);
            });

            modelBuilder.Entity<StockLevel>(b =>
            {
                b.ToTable("StockLevel");
                b.Property(x => x.LocationCode).HasMaxLength(10).IsRequired();
                b.Property(x => x.Sku).HasMaxLength(40).IsRequired();
                b.HasIndex(x => new { x.LocationCode, x.Sku }).IsUnique();
                b.HasIndex(x => x.Sku);
                b.Property(x => x.RowVersion).IsRowVersion();
                b.Ignore(x => x.Available);
            });

            modelBuilder.Entity<StockMovement>(b =>
            {
                b.ToTable("StockMovement");
                b.Property(x => x.LocationCode).HasMaxLength(10).IsRequired();
                b.Property(x => x.Sku).HasMaxLength(40).IsRequired();
                b.Property(x => x.Type).HasMaxLength(20).IsRequired();
                b.Property(x => x.Reference).HasMaxLength(100);
                b.Property(x => x.CreatedBy).HasMaxLength(100);
                b.HasIndex(x => new { x.LocationCode, x.Sku, x.CreatedUtc });
            });

            modelBuilder.Entity<Reservation>(b =>
            {
                b.ToTable("Reservation");
                b.Property(x => x.LocationCode).HasMaxLength(10).IsRequired();
                b.Property(x => x.Sku).HasMaxLength(40).IsRequired();
                b.Property(x => x.Status).HasMaxLength(20).IsRequired();
                b.HasIndex(x => new { x.OrderId, x.ShipmentId });
                b.HasIndex(x => new { x.Status, x.ExpiresUtc });
            });

            modelBuilder.Entity<ProcessedMessage>(b =>
            {
                b.ToTable("ProcessedMessage");
                b.HasKey(x => x.Key);
                b.Property(x => x.Key).HasMaxLength(200);
            });

            modelBuilder.Entity<ReplenishmentProposal>(b =>
            {
                b.ToTable("ReplenishmentProposal");
                b.Property(x => x.StoreCode).HasMaxLength(10).IsRequired();
                b.Property(x => x.FromLocation).HasMaxLength(10).IsRequired();
                b.Property(x => x.Status).HasMaxLength(20).IsRequired();
                b.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.ProposalId);
            });

            modelBuilder.Entity<ReplenishmentLine>(b =>
            {
                b.ToTable("ReplenishmentLine");
                b.Property(x => x.Sku).HasMaxLength(40).IsRequired();
            });

            modelBuilder.AddOutbox();
        }
    }
}
