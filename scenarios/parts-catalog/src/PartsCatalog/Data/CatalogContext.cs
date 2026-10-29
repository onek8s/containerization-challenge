using Microsoft.EntityFrameworkCore;
using PartsCatalog.Models;

namespace PartsCatalog.Data;

public class CatalogContext : DbContext
{
    public CatalogContext(DbContextOptions<CatalogContext> options) : base(options)
    {
    }

    public DbSet<Part> Parts => Set<Part>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Part>(e =>
        {
            e.HasIndex(p => p.PartNumber).IsUnique();
            // SQLite has no native decimal type; store as REAL so ORDER BY works.
            e.Property(p => p.UnitPrice).HasConversion<double>();
        });
    }
}
