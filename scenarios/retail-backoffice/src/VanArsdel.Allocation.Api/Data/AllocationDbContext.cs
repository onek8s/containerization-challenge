using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace VanArsdel.Allocation.Api.Data
{
    public class AllocationDecision
    {
        public long Id { get; set; }
        public Guid OrderId { get; set; }
        public string OrderNo { get; set; }
        public int Attempt { get; set; }
        public bool Success { get; set; }
        public string Strategy { get; set; }
        public string Reason { get; set; }
        public string DeliveryMethod { get; set; }
        public string Country { get; set; }
        public string ShipmentsJson { get; set; }
        public long ElapsedMs { get; set; }
        public DateTime CreatedUtc { get; set; }
    }

    public class AllocationRuleSet
    {
        public string Country { get; set; }
        public int MaxShipments { get; set; } = 2;
        public bool AllowShipFromStore { get; set; } = true;
        public bool AllowCrossBorderStores { get; set; }
        public int StoreMinimumLeft { get; set; }
        public int ReservationHours { get; set; } = 72;
        public DateTime UpdatedUtc { get; set; }
        public string UpdatedBy { get; set; }
    }

    public class AllocationDbContext : DbContext
    {
        public AllocationDbContext(DbContextOptions<AllocationDbContext> options) : base(options)
        {
        }

        public DbSet<AllocationDecision> Decisions => Set<AllocationDecision>();
        public DbSet<AllocationRuleSet> Rules => Set<AllocationRuleSet>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AllocationDecision>(b =>
            {
                b.ToTable("AllocationDecision");
                b.HasIndex(x => x.OrderId);
                b.Property(x => x.OrderNo).HasMaxLength(30);
                b.Property(x => x.Strategy).HasMaxLength(30);
                b.Property(x => x.Reason).HasMaxLength(500);
                b.Property(x => x.DeliveryMethod).HasMaxLength(20);
                b.Property(x => x.Country).HasMaxLength(2);
            });
            modelBuilder.Entity<AllocationRuleSet>(b =>
            {
                b.ToTable("AllocationRuleSet");
                b.HasKey(x => x.Country);
                b.Property(x => x.Country).HasMaxLength(2);
                b.Property(x => x.UpdatedBy).HasMaxLength(100);
            });
        }

        public static void Seed(AllocationDbContext db)
        {
            if (db.Rules.Any()) return;
            foreach (var c in new[] { "FI", "SE", "EE", "LV", "LT" })
            {
                db.Rules.Add(new AllocationRuleSet
                {
                    Country = c,
                    MaxShipments = c == "FI" || c == "SE" ? 2 : 1,
                    AllowShipFromStore = c == "FI" || c == "SE",
                    StoreMinimumLeft = c == "SE" ? 1 : 0,
                    UpdatedUtc = DateTime.UtcNow,
                    UpdatedBy = "seed",
                });
            }
            db.SaveChanges();
        }
    }
}
