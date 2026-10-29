using BatchRelease.Core.Model;
using Microsoft.EntityFrameworkCore;

namespace BatchRelease.Data;

public class BatchReleaseContext : DbContext
{
    public BatchReleaseContext(DbContextOptions<BatchReleaseContext> options) : base(options)
    {
    }

    public DbSet<ProductFamily> ProductFamilies => Set<ProductFamily>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Batch> Batches => Set<Batch>();
    public DbSet<RecordStep> RecordSteps => Set<RecordStep>();
    public DbSet<LabResult> LabResults => Set<LabResult>();
    public DbSet<Deviation> Deviations => Set<Deviation>();
    public DbSet<BatchDocument> Documents => Set<BatchDocument>();
    public DbSet<Decision> Decisions => Set<Decision>();
    public DbSet<Signer> Signers => Set<Signer>();
    public DbSet<ReleaseCertificateRecord> Certificates => Set<ReleaseCertificateRecord>();
    public DbSet<ApiClient> ApiClients => Set<ApiClient>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasDefaultSchema("br");

        b.Entity<ProductFamily>(e =>
        {
            e.ToTable("ProductFamily");
            e.HasKey(x => x.Code);
            e.Property(x => x.Code).HasMaxLength(20);
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.PolicySddl).HasMaxLength(4000);
            e.Property(x => x.PolicyChangedBy).HasMaxLength(128);
        });

        b.Entity<Product>(e =>
        {
            e.ToTable("Product");
            e.HasKey(x => x.Code);
            e.Property(x => x.Code).HasMaxLength(20);
            e.Property(x => x.Name).HasMaxLength(150);
            e.Property(x => x.FamilyCode).HasMaxLength(20);
            e.Property(x => x.Strength).HasMaxLength(50);
            e.Property(x => x.DosageForm).HasMaxLength(50);
            e.HasOne(x => x.Family).WithMany().HasForeignKey(x => x.FamilyCode);
        });

        b.Entity<Batch>(e =>
        {
            e.ToTable("Batch");
            e.Property(x => x.BatchNumber).HasMaxLength(12);
            e.HasIndex(x => x.BatchNumber).IsUnique();
            e.Property(x => x.ProductCode).HasMaxLength(20);
            e.Property(x => x.SiteCode).HasMaxLength(6);
            e.Property(x => x.MesReference).HasMaxLength(40);
            e.HasIndex(x => x.MesReference).IsUnique();
            e.Property(x => x.Quantity).HasPrecision(18, 3);
            e.Property(x => x.Unit).HasMaxLength(10);
            e.Property(x => x.CertificateNumber).HasMaxLength(20);
            e.Property(x => x.RowVersion).IsRowVersion();
            if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
                e.Property(x => x.RowVersion).HasDefaultValueSql("randomblob(8)");
            e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductCode);
            e.HasMany(x => x.Steps).WithOne().HasForeignKey(x => x.BatchId);
            e.HasMany(x => x.Results).WithOne().HasForeignKey(x => x.BatchId);
            e.HasMany(x => x.Deviations).WithOne().HasForeignKey(x => x.BatchId);
            e.HasMany(x => x.Documents).WithOne().HasForeignKey(x => x.BatchId);
            e.HasMany(x => x.Decisions).WithOne().HasForeignKey(x => x.BatchId);
        });

        b.Entity<RecordStep>(e =>
        {
            e.ToTable("RecordStep");
            e.Property(x => x.Operation).HasMaxLength(200);
            e.Property(x => x.PerformedBy).HasMaxLength(64);
            e.Property(x => x.VerifiedBy).HasMaxLength(64);
            e.Property(x => x.Comment).HasMaxLength(1000);
        });

        b.Entity<LabResult>(e =>
        {
            e.ToTable("LabResult");
            e.Property(x => x.TestCode).HasMaxLength(20);
            e.Property(x => x.TestName).HasMaxLength(150);
            e.Property(x => x.Specification).HasMaxLength(200);
            e.Property(x => x.LowerLimit).HasPrecision(18, 6);
            e.Property(x => x.UpperLimit).HasPrecision(18, 6);
            e.Property(x => x.Value).HasPrecision(18, 6);
            e.Property(x => x.TextResult).HasMaxLength(200);
            e.Property(x => x.Unit).HasMaxLength(20);
            e.Property(x => x.InvestigationNumber).HasMaxLength(20);
            e.Property(x => x.ApprovedBy).HasMaxLength(64);
        });

        b.Entity<Deviation>(e =>
        {
            e.ToTable("Deviation");
            e.Property(x => x.Number).HasMaxLength(20);
            e.Property(x => x.Title).HasMaxLength(300);
            e.Property(x => x.CapaNumber).HasMaxLength(20);
        });

        b.Entity<BatchDocument>(e =>
        {
            e.ToTable("BatchDocument");
            e.Property(x => x.Kind).HasMaxLength(40);
            e.Property(x => x.FileName).HasMaxLength(200);
            e.Property(x => x.ContentType).HasMaxLength(100);
            e.Property(x => x.Sha256).HasMaxLength(64).IsFixedLength();
            e.Property(x => x.VaultPath).HasMaxLength(400);
            e.Property(x => x.StoredBy).HasMaxLength(128);
        });

        b.Entity<Decision>(e =>
        {
            e.ToTable("Decision");
            e.Property(x => x.SignerAccount).HasMaxLength(128);
            e.Property(x => x.SignerSid).HasMaxLength(184);
            e.Property(x => x.SignerName).HasMaxLength(128);
            e.Property(x => x.Meaning).HasMaxLength(400);
            e.Property(x => x.Comment).HasMaxLength(2000);
            e.Property(x => x.CertificateNumber).HasMaxLength(20);
            e.Property(x => x.SignatureSha256).HasMaxLength(64).IsFixedLength();
        });

        b.Entity<Signer>(e =>
        {
            e.ToTable("Signer");
            e.Property(x => x.AccountName).HasMaxLength(128);
            e.Property(x => x.Sid).HasMaxLength(184);
            e.HasIndex(x => x.Sid);
            e.Property(x => x.DisplayName).HasMaxLength(128);
            e.Property(x => x.KeyName).HasMaxLength(128);
            e.Property(x => x.KeyProvider).HasMaxLength(128);
            e.Property(x => x.Thumbprint).HasMaxLength(40).IsFixedLength();
            e.HasIndex(x => x.Thumbprint).IsUnique();
            e.Property(x => x.EnrolledBy).HasMaxLength(128);
        });

        b.Entity<ReleaseCertificateRecord>(e =>
        {
            e.ToTable("ReleaseCertificate");
            e.Property(x => x.CertificateNumber).HasMaxLength(20);
            e.HasIndex(x => x.CertificateNumber).IsUnique();
            e.Property(x => x.SignerThumbprint).HasMaxLength(40).IsFixedLength();
            e.Property(x => x.VaultPath).HasMaxLength(400);
            e.Property(x => x.Sha256).HasMaxLength(64).IsFixedLength();
        });

        b.Entity<ApiClient>(e =>
        {
            e.ToTable("ApiClient");
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.KeyHash).HasMaxLength(64).IsFixedLength();
            e.Property(x => x.KeyPrefix).HasMaxLength(8);
            e.HasIndex(x => x.KeyPrefix);
            e.Property(x => x.SiteCode).HasMaxLength(6);
        });
    }
}
