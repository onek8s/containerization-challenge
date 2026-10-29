using ExpenseClaims.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExpenseClaims.Data;

public class ExpenseClaimsDbContext : DbContext
{
    public ExpenseClaimsDbContext(DbContextOptions<ExpenseClaimsDbContext> options) : base(options)
    {
    }

    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<ExpenseClaim> Claims => Set<ExpenseClaim>();
    public DbSet<ClaimLine> ClaimLines => Set<ClaimLine>();
    public DbSet<ClaimApproval> Approvals => Set<ClaimApproval>();
    public DbSet<ClaimEvent> ClaimEvents => Set<ClaimEvent>();
    public DbSet<Receipt> Receipts => Set<Receipt>();
    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();
    public DbSet<PaymentRun> PaymentRuns => Set<PaymentRun>();
    public DbSet<ClaimSequence> ClaimSequences => Set<ClaimSequence>();
    public DbSet<CompanyCard> CompanyCards => Set<CompanyCard>();
    public DbSet<CardTransaction> CardTransactions => Set<CardTransaction>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        builder.Properties<DateOnly>().HaveConversion<DateOnlyConverter>().HaveColumnType("date");
        builder.Properties<decimal>().HavePrecision(18, 2);
        builder.Properties<string>().HaveMaxLength(200);
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        var sqlite = Database.ProviderName?.EndsWith(".Sqlite", StringComparison.Ordinal) == true;

        if (!sqlite)
            b.HasDefaultSchema("exp");

        b.Entity<Employee>(e =>
        {
            e.HasIndex(x => x.EmployeeNumber).IsUnique();
            e.HasIndex(x => x.Upn).IsUnique();
            e.Property(x => x.EmployeeNumber).HasMaxLength(20);
            e.Property(x => x.ManagerEmployeeNumber).HasMaxLength(20);
            e.Property(x => x.CostCenter).HasMaxLength(20);
            e.Property(x => x.Iban).HasMaxLength(34);
            e.Property(x => x.Bic).HasMaxLength(11);
        });

        b.Entity<ExpenseClaim>(e =>
        {
            e.ToTable("Claims");
            e.HasIndex(x => x.Number).IsUnique();
            e.HasIndex(x => new { x.Status, x.SubmittedAt });
            e.Property(x => x.Number).HasMaxLength(20);
            e.Property(x => x.Purpose).HasMaxLength(500);
            e.Property(x => x.CostCenter).HasMaxLength(20);
            e.Property(x => x.ProjectCode).HasMaxLength(20);
            e.Property(x => x.JournalDocumentNo).HasMaxLength(20);
            e.HasIndex(x => new { x.Status, x.ExportedAt });
            e.Property(x => x.RowVersion).IsRowVersion();
            if (sqlite)
                e.Property(x => x.RowVersion).HasDefaultValueSql("randomblob(8)");
            e.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Lines).WithOne(x => x.Claim).HasForeignKey(x => x.ClaimId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Approvals).WithOne(x => x.Claim).HasForeignKey(x => x.ClaimId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Events).WithOne().HasForeignKey(x => x.ClaimId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ClaimLine>(e =>
        {
            e.HasIndex(x => new { x.ClaimId, x.LineNumber }).IsUnique();
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.Currency).HasMaxLength(3).IsFixedLength();
            e.Property(x => x.Country).HasMaxLength(2).IsFixedLength();
            e.Property(x => x.ExchangeRate).HasPrecision(18, 6);
            e.Property(x => x.Kilometres).HasPrecision(9, 1);
            e.HasOne(x => x.Receipt).WithMany().HasForeignKey(x => x.ReceiptId).OnDelete(DeleteBehavior.SetNull);
            e.Property(x => x.VatRate).HasPrecision(5, 2);
            e.HasOne(x => x.CardTransaction).WithMany().HasForeignKey(x => x.CardTransactionId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.CardTransactionId).IsUnique().HasFilter(sqlite ? null : "[CardTransactionId] IS NOT NULL");
        });

        b.Entity<ClaimApproval>(e =>
        {
            e.HasIndex(x => new { x.ApproverId, x.Decision });
            e.Property(x => x.Comment).HasMaxLength(1000);
            e.HasOne(x => x.Approver).WithMany().HasForeignKey(x => x.ApproverId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<ClaimEvent>(e =>
        {
            e.Property(x => x.Action).HasMaxLength(50);
            e.Property(x => x.Details).HasMaxLength(2000);
        });

        b.Entity<Receipt>(e =>
        {
            e.HasIndex(x => x.FileId).IsUnique();
            e.Property(x => x.Sha256).HasMaxLength(64);
            e.Property(x => x.ContentType).HasMaxLength(100);
            e.Property(x => x.StoragePath).HasMaxLength(400);
        });

        b.Entity<ExchangeRate>(e =>
        {
            e.HasKey(x => new { x.Date, x.Currency });
            e.Property(x => x.Currency).HasMaxLength(3).IsFixedLength();
            e.Property(x => x.UnitsPerEuro).HasPrecision(18, 6);
        });

        b.Entity<PaymentRun>(e =>
        {
            e.HasIndex(x => x.MessageId).IsUnique();
            e.Property(x => x.MessageId).HasMaxLength(35);
            e.Property(x => x.BankReference).HasMaxLength(100);
            e.Property(x => x.Error).HasMaxLength(2000);
        });

        b.Entity<CompanyCard>(e =>
        {
            e.HasIndex(x => x.MaskedNumber).IsUnique();
            e.Property(x => x.MaskedNumber).HasMaxLength(19);
            e.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<CardTransaction>(e =>
        {
            e.HasIndex(x => x.Reference).IsUnique();
            e.HasIndex(x => new { x.CompanyCardId, x.TransactionDate });
            e.Property(x => x.Reference).HasMaxLength(40);
            e.Property(x => x.MerchantCity).HasMaxLength(60);
            e.Property(x => x.MerchantCountry).HasMaxLength(3);
            e.Property(x => x.MerchantCategoryCode).HasMaxLength(4).IsFixedLength();
            e.Property(x => x.OriginalCurrency).HasMaxLength(3).IsFixedLength();
            e.Property(x => x.SourceFile).HasMaxLength(260);
        });

        b.Entity<ClaimSequence>(e =>
        {
            e.HasKey(x => x.Year);
            e.Property(x => x.Year).ValueGeneratedNever();
            e.Property(x => x.LastNumber).IsConcurrencyToken();
        });
    }
}
