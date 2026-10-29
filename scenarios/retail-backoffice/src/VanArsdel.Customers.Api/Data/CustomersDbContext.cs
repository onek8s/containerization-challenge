using System;
using Microsoft.EntityFrameworkCore;

namespace VanArsdel.Customers.Api.Data
{
    public class CustomerDocumentRow
    {
        public string Id { get; set; }
        public int Version { get; set; }
        public string Email { get; set; }
        public string LastName { get; set; }
        public string Phone { get; set; }
        public string CardNo { get; set; }
        public string Json { get; set; }
        public DateTime UpdatedUtc { get; set; }
    }

    public class CustomersDbContext : DbContext
    {
        public CustomersDbContext(DbContextOptions<CustomersDbContext> options) : base(options)
        {
        }

        public DbSet<CustomerDocumentRow> Documents { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CustomerDocumentRow>(b =>
            {
                b.ToTable("CustomerDocument");
                b.HasKey(x => x.Id);
                b.Property(x => x.Id).HasMaxLength(40);
                b.Property(x => x.Email).HasMaxLength(200);
                b.HasIndex(x => x.Email).IsUnique();
                b.Property(x => x.LastName).HasMaxLength(100);
                b.HasIndex(x => x.LastName);
                b.Property(x => x.Phone).HasMaxLength(30);
                b.Property(x => x.CardNo).HasMaxLength(20);
                b.HasIndex(x => x.CardNo);
                b.Property(x => x.Version).IsConcurrencyToken();
                b.Property(x => x.Json).IsRequired();
            });
        }
    }
}
