using Microsoft.EntityFrameworkCore;
using VanArsdel.BuildingBlocks.Outbox;
using VanArsdel.Catalog.Api.Domain;

namespace VanArsdel.Catalog.Api.Data
{
    public class CatalogDbContext : DbContext, IOutboxContext
    {
        public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options)
        {
        }

        public DbSet<Product> Products { get; set; }
        public DbSet<Sku> Skus { get; set; }
        public DbSet<Brand> Brands { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<ProductImage> ProductImages { get; set; }
        public DbSet<OutboxMessage> OutboxMessages { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Product>(b =>
            {
                b.ToTable("Product");
                b.HasKey(x => x.Id);
                b.Property(x => x.StyleCode).HasMaxLength(20).IsRequired();
                b.HasIndex(x => x.StyleCode).IsUnique();
                b.Property(x => x.Name).HasMaxLength(200).IsRequired();
                b.Property(x => x.Description).HasMaxLength(4000);
                b.Property(x => x.Season).HasMaxLength(10);
                b.Property(x => x.Gender).HasMaxLength(10);
                b.Property(x => x.Material).HasMaxLength(200);
                b.Property(x => x.CareInstructions).HasMaxLength(500);
                b.Property(x => x.VatClass).HasMaxLength(20);
                b.Property(x => x.RecommendedPriceEur).HasColumnType("decimal(18,2)");
                b.Property(x => x.RecommendedPriceSek).HasColumnType("decimal(18,2)");
                b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
                b.Property(x => x.RowVersion).IsRowVersion();
                b.HasOne(x => x.Brand).WithMany().HasForeignKey(x => x.BrandId).OnDelete(DeleteBehavior.Restrict);
                b.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
                b.HasMany(x => x.Skus).WithOne().HasForeignKey(x => x.ProductId);
                b.HasMany(x => x.Images).WithOne().HasForeignKey(x => x.ProductId);
            });

            modelBuilder.Entity<Sku>(b =>
            {
                b.ToTable("Sku");
                b.Property(x => x.SkuCode).HasMaxLength(40).IsRequired();
                b.HasIndex(x => x.SkuCode).IsUnique();
                b.Property(x => x.Ean).HasMaxLength(13);
                b.HasIndex(x => x.Ean);
                b.Property(x => x.ColourCode).HasMaxLength(10);
                b.Property(x => x.ColourName).HasMaxLength(50);
                b.Property(x => x.Size).HasMaxLength(10);
                b.Property(x => x.CostPrice).HasColumnType("decimal(18,2)");
                b.Property(x => x.WeightKg).HasColumnType("decimal(9,3)");
            });

            modelBuilder.Entity<ProductImage>(b =>
            {
                b.ToTable("ProductImage");
                b.Property(x => x.Url).HasMaxLength(500).IsRequired();
                b.Property(x => x.ColourCode).HasMaxLength(10);
            });

            modelBuilder.Entity<Brand>(b =>
            {
                b.ToTable("Brand");
                b.Property(x => x.Code).HasMaxLength(10).IsRequired();
                b.HasIndex(x => x.Code).IsUnique();
                b.Property(x => x.Name).HasMaxLength(100).IsRequired();
            });

            modelBuilder.Entity<Category>(b =>
            {
                b.ToTable("Category");
                b.Property(x => x.Code).HasMaxLength(20).IsRequired();
                b.HasIndex(x => x.Code).IsUnique();
                b.Property(x => x.Name).HasMaxLength(100).IsRequired();
                b.Property(x => x.ErpItemGroup).HasMaxLength(20);
                b.Property(x => x.SizeScale).HasMaxLength(20);
                b.HasOne(x => x.Parent).WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.AddOutbox();
        }
    }
}
