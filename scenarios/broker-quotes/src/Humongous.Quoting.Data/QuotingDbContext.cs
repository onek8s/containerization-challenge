using System.Text.Json;
using Humongous.Quoting.Contracts;
using Humongous.Quoting.Data.Entities;
using Humongous.Quoting.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Humongous.Quoting.Data;

public class QuotingDbContext(DbContextOptions<QuotingDbContext> options) : DbContext(options)
{
    public DbSet<Agency> Agencies => Set<Agency>();
    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<QuoteReferral> QuoteReferrals => Set<QuoteReferral>();
    public DbSet<QuoteEvent> QuoteEvents => Set<QuoteEvent>();
    public DbSet<Policy> Policies => Set<Policy>();
    public DbSet<PolicyTransaction> PolicyTransactions => Set<PolicyTransaction>();
    public DbSet<StoredDocument> Documents => Set<StoredDocument>();
    public DbSet<BordereauRun> BordereauRuns => Set<BordereauRun>();
    public DbSet<NumberSequence> NumberSequences => Set<NumberSequence>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        builder.Properties<decimal>().HavePrecision(18, 2);
        builder.Properties<string>().HaveMaxLength(200);
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Agency>(e =>
        {
            e.ToTable("Agencies");
            e.HasKey(x => x.Code);
            e.Property(x => x.Code).HasMaxLength(10);
            e.Property(x => x.FcaNumber).HasMaxLength(10);
            e.Property(x => x.CommissionRate).HasPrecision(5, 4);
        });

        b.Entity<Quote>(e =>
        {
            e.ToTable("Quotes");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Reference).IsUnique();
            e.HasIndex(x => new { x.AgencyCode, x.Status });
            e.HasIndex(x => new { x.Status, x.ReferredUtc });
            e.Property(x => x.Reference).HasMaxLength(20);
            e.Property(x => x.AgencyCode).HasMaxLength(10);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.ProductVersion).HasMaxLength(10);
            e.Property(x => x.Postcode).HasMaxLength(10);
            e.Property(x => x.TradeCode).HasMaxLength(10);
            e.Property(x => x.CommissionRate).HasPrecision(5, 4);
            e.Property(x => x.UnderwriterNote).HasMaxLength(2000);
            e.Property(x => x.RatingError).HasMaxLength(1000);
            e.Property(x => x.Risk).HasJsonConversion().HasColumnName("RiskJson").HasMaxLength(-1);
            e.Property(x => x.Premium).HasJsonConversion().HasColumnName("PremiumJson").HasMaxLength(-1);
            e.Property(x => x.Loadings).HasJsonConversion().HasColumnName("LoadingsJson").HasMaxLength(1000);
            e.Property(x => x.Clauses).HasJsonConversion().HasColumnName("ClausesJson").HasMaxLength(1000);
            e.Property(x => x.ConcurrencyStamp).IsConcurrencyToken();
            e.HasOne(x => x.Agency).WithMany().HasForeignKey(x => x.AgencyCode).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Referrals).WithOne().HasForeignKey(x => x.QuoteId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Events).WithOne().HasForeignKey(x => x.QuoteId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<QuoteReferral>(e =>
        {
            e.ToTable("QuoteReferrals");
            e.Property(x => x.Code).HasMaxLength(10);
            e.Property(x => x.Text).HasMaxLength(400);
            e.Property(x => x.Source).HasMaxLength(20);
        });

        b.Entity<QuoteEvent>(e =>
        {
            e.ToTable("QuoteEvents");
            e.Property(x => x.Event).HasMaxLength(40);
            e.Property(x => x.Detail).HasMaxLength(2000);
            e.HasIndex(x => x.QuoteId);
        });

        b.Entity<Policy>(e =>
        {
            e.ToTable("Policies");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.PolicyNumber).IsUnique();
            e.HasIndex(x => new { x.Status, x.ExpiryDate });
            e.HasIndex(x => x.AgencyCode);
            e.Property(x => x.PolicyNumber).HasMaxLength(20);
            e.Property(x => x.AgencyCode).HasMaxLength(10);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.ProductVersion).HasMaxLength(10);
            e.Property(x => x.Postcode).HasMaxLength(10);
            e.Property(x => x.TradeCode).HasMaxLength(10);
            e.Property(x => x.CommissionRate).HasPrecision(5, 4);
            e.Property(x => x.Risk).HasJsonConversion().HasColumnName("RiskJson").HasMaxLength(-1);
            e.Property(x => x.Premium).HasJsonConversion().HasColumnName("PremiumJson").HasMaxLength(-1);
            e.Property(x => x.Clauses).HasJsonConversion().HasColumnName("ClausesJson").HasMaxLength(1000);
            e.Property(x => x.ConcurrencyStamp).IsConcurrencyToken();
            e.HasOne(x => x.Agency).WithMany().HasForeignKey(x => x.AgencyCode).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Transactions).WithOne(x => x.Policy).HasForeignKey(x => x.PolicyId);
        });

        b.Entity<PolicyTransaction>(e =>
        {
            e.ToTable("PolicyTransactions");
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Reason).HasMaxLength(400);
            e.Property(x => x.SectionGross).HasJsonConversion().HasColumnName("SectionGrossJson").HasMaxLength(1000);
            e.HasIndex(x => new { x.CreatedUtc, x.BordereauRunId });
        });

        b.Entity<StoredDocument>(e =>
        {
            e.ToTable("Documents");
            e.Property(x => x.DocumentType).HasMaxLength(30);
            e.Property(x => x.AgencyCode).HasMaxLength(10);
            e.Property(x => x.ContentType).HasMaxLength(100);
            e.Property(x => x.Sha256).HasMaxLength(64);
            e.Property(x => x.Content).HasMaxLength(-1);
            e.HasIndex(x => x.QuoteId);
            e.HasIndex(x => x.PolicyId);
        });

        b.Entity<BordereauRun>(e =>
        {
            e.ToTable("BordereauRuns");
            e.Property(x => x.Period).HasMaxLength(7);
            e.Property(x => x.Status).HasMaxLength(20);
            e.Property(x => x.Error).HasMaxLength(2000);
            e.Property(x => x.Content).HasMaxLength(-1);
            e.HasIndex(x => x.Period);
        });

        b.Entity<NumberSequence>(e =>
        {
            e.ToTable("NumberSequences");
            e.HasKey(x => x.Name);
            e.Property(x => x.Name).HasMaxLength(20);
        });
    }
}

internal static class JsonColumnExtensions
{
    public static Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<T> HasJsonConversion<T>(
        this Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<T> property)
    {
        var converter = new ValueConverter<T, string>(
            v => JsonSerializer.Serialize(v, QuotingJson.Options),
            v => JsonSerializer.Deserialize<T>(v, QuotingJson.Options)!);
        var comparer = new ValueComparer<T>(
            (a, b) => JsonSerializer.Serialize(a, QuotingJson.Options) == JsonSerializer.Serialize(b, QuotingJson.Options),
            v => v == null ? 0 : JsonSerializer.Serialize(v, QuotingJson.Options).GetHashCode(),
            v => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(v, QuotingJson.Options), QuotingJson.Options)!);
        property.HasConversion(converter, comparer);
        return property;
    }
}
