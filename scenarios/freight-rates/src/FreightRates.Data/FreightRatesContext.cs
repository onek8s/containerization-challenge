using FreightRates.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FreightRates.Data;

public class FreightRatesContext : DbContext
{
    public FreightRatesContext(DbContextOptions<FreightRatesContext> options)
        : base(options)
    {
    }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Accessorial> Accessorials => Set<Accessorial>();
    public DbSet<FuelPrice> FuelPrices => Set<FuelPrice>();
    public DbSet<FuelSurchargeStep> FuelSurchargeSteps => Set<FuelSurchargeStep>();
    public DbSet<Quote> Quotes => Set<Quote>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(e =>
        {
            e.HasIndex(c => c.Code).IsUnique();
            e.Property(c => c.Code).HasMaxLength(12);
            e.Property(c => c.Name).HasMaxLength(100);
            e.Property(c => c.ApiKeyHash).HasMaxLength(64);
            e.Property(c => c.DiscountPercent).HasPrecision(5, 2);
            e.Property(c => c.MinimumCharge).HasPrecision(10, 2);
            e.Property(c => c.FuelSurchargeOverridePercent).HasPrecision(5, 2);
            e.Property(c => c.ModifiedBy).HasMaxLength(100);
        });

        modelBuilder.Entity<Accessorial>(e =>
        {
            e.HasKey(a => a.Code);
            e.Property(a => a.Code).HasMaxLength(8);
            e.Property(a => a.Description).HasMaxLength(100);
            e.Property(a => a.Amount).HasPrecision(10, 2);
            e.Property(a => a.PerCwt).HasPrecision(10, 2);
        });

        modelBuilder.Entity<FuelPrice>(e =>
        {
            e.HasIndex(f => f.WeekStarting).IsUnique();
            e.Property(f => f.DieselPrice).HasPrecision(6, 3);
        });

        modelBuilder.Entity<FuelSurchargeStep>(e =>
        {
            e.Property(f => f.DieselPriceFrom).HasPrecision(6, 3);
            e.Property(f => f.Percent).HasPrecision(5, 2);
        });

        modelBuilder.Entity<Quote>(e =>
        {
            e.HasIndex(q => q.Number).IsUnique();
            e.HasIndex(q => new { q.CustomerId, q.CreatedUtc });
            e.Property(q => q.Number).HasMaxLength(16);
            e.Property(q => q.CustomerReference).HasMaxLength(40);
            e.Property(q => q.OriginZip).HasMaxLength(5);
            e.Property(q => q.DestinationZip).HasMaxLength(5);
            e.Property(q => q.AccessorialCodes).HasMaxLength(200);
            e.Property(q => q.TariffName).HasMaxLength(12);
            e.Property(q => q.OriginTerminal).HasMaxLength(8);
            e.Property(q => q.DestinationTerminal).HasMaxLength(8);
            foreach (var p in new[] { nameof(Quote.GrossCharge), nameof(Quote.Discount), nameof(Quote.NetLinehaul), nameof(Quote.FuelSurcharge), nameof(Quote.AccessorialTotal), nameof(Quote.Total) })
                e.Property(p).HasPrecision(12, 2);
            e.Property(q => q.FuelSurchargePercent).HasPrecision(5, 2);
        });
    }
}
