using CityPermits.Domain.Model;
using Microsoft.EntityFrameworkCore;

namespace CityPermits.Data;

public class PermitsDbContext : DbContext
{
    public PermitsDbContext(DbContextOptions<PermitsDbContext> options) : base(options) { }

    public DbSet<PermitCase> Cases => Set<PermitCase>();
    public DbSet<Applicant> Applicants => Set<Applicant>();
    public DbSet<FeeLine> FeeLines => Set<FeeLine>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<CaseEvent> CaseEvents => Set<CaseEvent>();
    public DbSet<CaseDocument> CaseDocuments => Set<CaseDocument>();
    public DbSet<Condition> Conditions => Set<Condition>();
    public DbSet<Consultation> Consultations => Set<Consultation>();
    public DbSet<Inspection> Inspections => Set<Inspection>();
    public DbSet<ZoneTariff> ZoneTariffs => Set<ZoneTariff>();
    public DbSet<SequenceCounter> Sequences => Set<SequenceCounter>();
    public DbSet<Holiday> Holidays => Set<Holiday>();
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();
    public DbSet<PaymentNotification> PaymentNotifications => Set<PaymentNotification>();
    public DbSet<IntakeItem> IntakeItems => Set<IntakeItem>();
    public DbSet<JobRun> JobRuns => Set<JobRun>();
    public DbSet<StandardCondition> StandardConditions => Set<StandardCondition>();

    public bool IsSqlServer => Database.ProviderName == "Microsoft.EntityFrameworkCore.SqlServer";

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasDefaultSchema(IsSqlServerModel ? "permits" : null);

        b.Entity<PermitCase>(e =>
        {
            e.ToTable("Cases");
            e.HasIndex(x => x.Reference).IsUnique();
            e.HasIndex(x => new { x.Status, x.Kind });
            e.HasIndex(x => x.ValidTo);
            e.HasIndex(x => x.Uprn);
            e.Property(x => x.Reference).HasMaxLength(20).IsRequired();
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Uprn).HasMaxLength(12);
            e.Property(x => x.ParcelId).HasMaxLength(20);
            e.Property(x => x.SiteAddress).HasMaxLength(200);
            e.Property(x => x.AssignedOfficer).HasMaxLength(100);
            e.Property(x => x.Team).HasMaxLength(50);
            e.Property(x => x.Ward).HasMaxLength(50);
            e.Property(x => x.DecidedBy).HasMaxLength(100);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasOne(x => x.Applicant).WithMany(a => a.Cases).HasForeignKey(x => x.ApplicantId);
            e.OwnsOne(x => x.Building, o =>
            {
                o.Property(p => p.EstimatedCost).HasPrecision(14, 2);
                o.Property(p => p.UseClass).HasMaxLength(20);
                o.Property(p => p.AgentName).HasMaxLength(120);
                o.Property(p => p.AgentEmail).HasMaxLength(200);
            });
            e.OwnsOne(x => x.Business, o =>
            {
                o.Property(p => p.TradingName).HasMaxLength(120);
                o.Property(p => p.UseClass).HasMaxLength(20);
            });
            e.OwnsOne(x => x.Parking, o =>
            {
                o.Property(p => p.Registration).HasMaxLength(10);
                o.Property(p => p.ZoneCode).HasMaxLength(4);
                o.Property(p => p.PermitSerial).HasMaxLength(20);
            });
            e.HasMany(x => x.Fees).WithOne().HasForeignKey(f => f.CaseId);
            e.HasMany(x => x.Payments).WithOne().HasForeignKey(f => f.CaseId);
            e.HasMany(x => x.Events).WithOne().HasForeignKey(f => f.CaseId);
            e.HasMany(x => x.Documents).WithOne().HasForeignKey(f => f.CaseId);
            e.HasMany(x => x.Conditions).WithOne().HasForeignKey(f => f.CaseId);
            e.HasMany(x => x.Inspections).WithOne().HasForeignKey(f => f.CaseId);
            e.HasMany(x => x.Consultations).WithOne().HasForeignKey(f => f.CaseId);
            e.Ignore(x => x.TotalFees);
            e.Ignore(x => x.TotalPaid);
            e.Ignore(x => x.Balance);
        });

        b.Entity<Applicant>(e =>
        {
            e.ToTable("Applicants");
            e.HasIndex(x => x.EidSubject).IsUnique().HasFilter(IsSqlServerModel ? "[EidSubject] IS NOT NULL" : "EidSubject IS NOT NULL");
            e.Property(x => x.EidSubject).HasMaxLength(100);
            e.Property(x => x.PersonalIdHash).HasMaxLength(64);
            e.Property(x => x.DisplayName).HasMaxLength(150).IsRequired();
            e.Property(x => x.Email).HasMaxLength(200);
            e.Property(x => x.Postcode).HasMaxLength(10);
        });

        b.Entity<FeeLine>(e =>
        {
            e.ToTable("FeeLines");
            e.Property(x => x.Amount).HasPrecision(12, 2);
            e.Property(x => x.VatRate).HasPrecision(5, 4);
            e.Property(x => x.Code).HasMaxLength(10);
        });
        b.Entity<Payment>(e =>
        {
            e.ToTable("Payments");
            e.Property(x => x.Amount).HasPrecision(12, 2);
            e.HasIndex(x => x.ProviderReference).IsUnique();
            e.Property(x => x.ProviderReference).HasMaxLength(64);
            e.Property(x => x.SessionId).HasMaxLength(64);
            e.Property(x => x.Currency).HasMaxLength(3);
        });
        b.Entity<CaseEvent>(e => { e.ToTable("CaseEvents"); e.HasIndex(x => new { x.CaseId, x.AtUtc }); });
        b.Entity<CaseDocument>(e =>
        {
            e.ToTable("CaseDocuments");
            e.HasIndex(x => x.ArchiveKey).IsUnique();
            e.HasIndex(x => x.RecordsFiledUtc);
            e.Property(x => x.FileName).HasMaxLength(255);
            e.Property(x => x.Sha256).HasMaxLength(64);
        });
        b.Entity<Condition>(e => e.ToTable("Conditions"));
        b.Entity<Consultation>(e => e.ToTable("Consultations"));
        b.Entity<Inspection>(e =>
        {
            e.ToTable("Inspections");
            e.HasIndex(x => new { x.Inspector, x.ScheduledDate });
            e.HasIndex(x => x.ClientMutationId).IsUnique().HasFilter(IsSqlServerModel ? "[ClientMutationId] IS NOT NULL" : "ClientMutationId IS NOT NULL");
        });
        b.Entity<ZoneTariff>(e =>
        {
            e.ToTable("ZoneTariffs");
            e.HasKey(x => x.ZoneCode);
            e.Property(x => x.ResidentAnnual).HasPrecision(10, 2);
            e.Property(x => x.BusinessAnnual).HasPrecision(10, 2);
            e.Property(x => x.VisitorBookOf20).HasPrecision(10, 2);
        });
        b.Entity<SequenceCounter>(e => { e.ToTable("Sequences"); e.HasKey(x => new { x.Name, x.Year }); });
        b.Entity<Holiday>(e => { e.ToTable("Holidays"); e.HasKey(x => x.Date); });
        b.Entity<OutboxMessage>(e => { e.ToTable("Outbox"); e.HasIndex(x => new { x.ProcessedUtc, x.Id }); });
        b.Entity<PaymentNotification>(e => { e.ToTable("PaymentNotifications"); e.HasIndex(x => x.EventId).IsUnique(); });
        b.Entity<IntakeItem>(e => { e.ToTable("IntakeItems"); e.HasIndex(x => x.SourceFile).IsUnique(); });
        b.Entity<JobRun>(e => e.ToTable("JobRuns"));
        b.Entity<StandardCondition>(e => { e.ToTable("StandardConditions"); e.HasKey(x => x.Code); });

        if (!IsSqlServerModel)
        {
            // SQLite (development): rowversion is maintained by the application.
            b.Entity<PermitCase>().Property(x => x.RowVersion).IsRowVersion().HasDefaultValue(Array.Empty<byte>()).ValueGeneratedNever().IsConcurrencyToken();
        }
    }

    private bool IsSqlServerModel => Database.ProviderName == "Microsoft.EntityFrameworkCore.SqlServer";

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        TouchRowVersions();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        TouchRowVersions();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void TouchRowVersions()
    {
        if (IsSqlServerModel) return;
        foreach (var e in ChangeTracker.Entries<PermitCase>().Where(e => e.State is EntityState.Added or EntityState.Modified))
            e.Entity.RowVersion = BitConverter.GetBytes(DateTime.UtcNow.Ticks);
    }
}

public class OutboxMessage
{
    public long Id { get; set; }
    public string Type { get; set; } = "";
    public string Payload { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
    public DateTime? ProcessedUtc { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
}

public class PaymentNotification
{
    public long Id { get; set; }
    public string EventId { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTime ReceivedUtc { get; set; }
    public bool SignatureValid { get; set; }
    public string? Outcome { get; set; }
}

public class IntakeItem
{
    public int Id { get; set; }
    public string SourceFile { get; set; } = "";
    public int Pages { get; set; }
    public string? FormCode { get; set; }
    public double Confidence { get; set; }
    public string? ExtractedJson { get; set; }
    public string? OcrText { get; set; }
    public int? CaseId { get; set; }
    public string Status { get; set; } = "New";
    public DateTime ReceivedUtc { get; set; }
    public DateTime? ProcessedUtc { get; set; }
    public string? Error { get; set; }
}

public class JobRun
{
    public long Id { get; set; }
    public string Job { get; set; } = "";
    public DateTime StartedUtc { get; set; }
    public DateTime? FinishedUtc { get; set; }
    public string Host { get; set; } = "";
    public bool Success { get; set; }
    public int Items { get; set; }
    public string? Message { get; set; }
}

public class StandardCondition
{
    public string Code { get; set; } = "";
    public string Text { get; set; } = "";
    public string AppliesTo { get; set; } = "";
}
