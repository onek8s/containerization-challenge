using System;
using Microsoft.EntityFrameworkCore;

namespace VanArsdel.ErpConnector.Data
{
    public static class PostingStatus
    {
        public const string Posted = "Posted";
        public const string Failed = "Failed";
    }

    /// <summary>One row per document sent to the ERP. Used for idempotency and by finance support to find failures.</summary>
    public class ErpPosting
    {
        public long Id { get; set; }
        public string Kind { get; set; }
        public string ExternalReference { get; set; }
        public string DocumentNo { get; set; }
        public string Status { get; set; }
        public int Attempts { get; set; }
        public string Error { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime UpdatedUtc { get; set; }
    }

    public class ErpDbContext : DbContext
    {
        public ErpDbContext(DbContextOptions<ErpDbContext> options) : base(options)
        {
        }

        public DbSet<ErpPosting> Postings { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ErpPosting>(b =>
            {
                b.ToTable("ErpPosting");
                b.HasIndex(x => new { x.Kind, x.ExternalReference }).IsUnique();
                b.HasIndex(x => x.Status);
                b.Property(x => x.Kind).HasMaxLength(30).IsRequired();
                b.Property(x => x.ExternalReference).HasMaxLength(60).IsRequired();
                b.Property(x => x.DocumentNo).HasMaxLength(30);
                b.Property(x => x.Status).HasMaxLength(10).IsRequired();
                b.Property(x => x.Error).HasMaxLength(1000);
            });
        }
    }
}
