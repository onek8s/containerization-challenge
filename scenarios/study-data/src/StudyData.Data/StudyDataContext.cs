using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using StudyData.Core.Coding;
using StudyData.Core.Design;
using StudyData.Core.Documents;
using StudyData.Core.Labs;
using StudyData.Core.Queries;
using StudyData.Core.Subjects;

namespace StudyData.Data;

public class StudyDataContext : IdentityDbContext<SiteUser>
{
    public StudyDataContext(DbContextOptions<StudyDataContext> options) : base(options)
    {
    }

    public DbSet<Study> Studies => Set<Study>();
    public DbSet<Site> Sites => Set<Site>();
    public DbSet<Arm> Arms => Set<Arm>();
    public DbSet<VisitDefinition> VisitDefinitions => Set<VisitDefinition>();
    public DbSet<FormDefinition> FormDefinitions => Set<FormDefinition>();
    public DbSet<FieldDefinition> FieldDefinitions => Set<FieldDefinition>();
    public DbSet<EditCheckDefinition> EditChecks => Set<EditCheckDefinition>();
    public DbSet<CalculationWorkbook> Workbooks => Set<CalculationWorkbook>();
    public DbSet<CalculationInput> WorkbookInputs => Set<CalculationInput>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<Visit> Visits => Set<Visit>();
    public DbSet<FormInstance> Forms => Set<FormInstance>();
    public DbSet<FieldValue> Values => Set<FieldValue>();
    public DbSet<AuditEntry> Audit => Set<AuditEntry>();
    public DbSet<Query> Queries => Set<Query>();
    public DbSet<QueryMessage> QueryMessages => Set<QueryMessage>();
    public DbSet<CodingItem> Coding => Set<CodingItem>();
    public DbSet<LabResult> LabResults => Set<LabResult>();
    public DbSet<LabTransferFile> LabFiles => Set<LabTransferFile>();
    public DbSet<SourceDocument> SourceDocuments => Set<SourceDocument>();
    public DbSet<GeneratedDocument> GeneratedDocuments => Set<GeneratedDocument>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<SiteAssignment> SiteAssignments => Set<SiteAssignment>();
    public DbSet<RandomisationSlot> RandomisationSlots => Set<RandomisationSlot>();
    public DbSet<SponsorClient> SponsorClients => Set<SponsorClient>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.HasDefaultSchema(Database.IsSqlServer() ? "edc" : null);

        b.Entity<Study>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(32);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        });
        b.Entity<Site>(e =>
        {
            e.HasIndex(x => new { x.StudyId, x.Number }).IsUnique();
            e.HasOne(x => x.Study).WithMany(x => x.Sites).HasForeignKey(x => x.StudyId);
        });
        b.Entity<Arm>().HasIndex(x => new { x.StudyId, x.Code }).IsUnique();
        b.Entity<VisitDefinition>().HasIndex(x => new { x.StudyId, x.Code }).IsUnique();
        b.Entity<FormDefinition>(e =>
        {
            e.HasIndex(x => new { x.StudyId, x.Code }).IsUnique();
            e.HasMany(x => x.Fields).WithOne().HasForeignKey(x => x.FormDefinitionId);
            e.HasMany(x => x.EditChecks).WithOne().HasForeignKey(x => x.FormDefinitionId);
        });
        b.Entity<FieldDefinition>(e =>
        {
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Min).HasPrecision(18, 4);
            e.Property(x => x.Max).HasPrecision(18, 4);
        });
        b.Entity<EditCheckDefinition>().Property(x => x.Severity).HasConversion<string>().HasMaxLength(16);
        b.Entity<CalculationWorkbook>(e =>
        {
            e.HasIndex(x => new { x.StudyId, x.Code, x.Version }).IsUnique();
            e.HasMany(x => x.Inputs).WithOne().HasForeignKey(x => x.CalculationWorkbookId);
        });

        b.Entity<Subject>(e =>
        {
            e.HasIndex(x => new { x.StudyId, x.Number }).IsUnique();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.HasOne(x => x.Study).WithMany().HasForeignKey(x => x.StudyId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Arm).WithMany().HasForeignKey(x => x.ArmId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<Visit>(e =>
        {
            e.HasIndex(x => new { x.SubjectId, x.VisitDefinitionId, x.Repeat }).IsUnique();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.HasOne(x => x.Subject).WithMany(x => x.Visits).HasForeignKey(x => x.SubjectId);
            e.HasOne(x => x.Definition).WithMany().HasForeignKey(x => x.VisitDefinitionId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<FormInstance>(e =>
        {
            e.HasIndex(x => new { x.VisitId, x.FormDefinitionId, x.Row }).IsUnique();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.HasOne(x => x.Visit).WithMany(x => x.Forms).HasForeignKey(x => x.VisitId);
            e.HasOne(x => x.Definition).WithMany().HasForeignKey(x => x.FormDefinitionId).OnDelete(DeleteBehavior.Restrict);
            if (Database.IsSqlServer())
            {
                e.Property(x => x.RowVersion).IsRowVersion();
            }
            else
            {
                e.Property(x => x.RowVersion).IsConcurrencyToken();
            }
        });
        b.Entity<FieldValue>(e =>
        {
            e.HasIndex(x => new { x.FormInstanceId, x.FieldCode }).IsUnique();
            e.HasOne(x => x.Form).WithMany(x => x.Values).HasForeignKey(x => x.FormInstanceId);
            e.Property(x => x.Value).HasMaxLength(2000);
        });
        b.Entity<AuditEntry>(e =>
        {
            e.HasIndex(x => new { x.SubjectId, x.At });
            e.HasIndex(x => new { x.EntityType, x.EntityKey });
        });
        b.Entity<Query>(e =>
        {
            e.Property(x => x.State).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Origin).HasConversion<string>().HasMaxLength(16);
            e.HasIndex(x => new { x.SubjectId, x.State });
            e.HasMany(x => x.Messages).WithOne().HasForeignKey(x => x.QueryId);
        });
        b.Entity<QueryMessage>().Property(x => x.NewState).HasConversion<string>().HasMaxLength(16);
        b.Entity<CodingItem>(e =>
        {
            e.Property(x => x.State).HasConversion<string>().HasMaxLength(16);
            e.HasIndex(x => new { x.FormInstanceId, x.FieldCode }).IsUnique();
            e.HasIndex(x => x.State);
        });
        b.Entity<LabResult>(e =>
        {
            e.Property(x => x.Flag).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.LowerLimit).HasPrecision(18, 4);
            e.Property(x => x.UpperLimit).HasPrecision(18, 4);
            e.HasIndex(x => new { x.Accession, x.TestCode }).IsUnique();
        });
        b.Entity<LabTransferFile>().HasIndex(x => x.Sha256).IsUnique();
        b.Entity<GeneratedDocument>(e =>
        {
            e.Property(x => x.State).HasConversion<string>().HasMaxLength(16);
            e.HasIndex(x => new { x.Kind, x.State });
        });
        b.Entity<Notification>().HasIndex(x => new { x.Delivered, x.Id });
        b.Entity<SiteAssignment>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.SiteId }).IsUnique();
            e.HasOne<SiteUser>().WithMany(u => u.Sites).HasForeignKey(x => x.UserId);
        });
        b.Entity<RandomisationSlot>().HasIndex(x => new { x.StudyId, x.SiteNumber, x.Stratum, x.Sequence }).IsUnique();
        b.Entity<SponsorClient>().HasIndex(x => x.ClientId).IsUnique();
    }
}

/// <summary>A position in the pre-generated randomisation list of a site and stratum.</summary>
public class RandomisationSlot
{
    public int Id { get; set; }
    public int StudyId { get; set; }
    public string SiteNumber { get; set; } = "";
    public string Stratum { get; set; } = "";
    public int Sequence { get; set; }
    public string RandomisationNumber { get; set; } = "";
    public string ArmCode { get; set; } = "";
    public int? SubjectId { get; set; }
    public DateTime? UsedAt { get; set; }
}

/// <summary>Sponsor system allowed to call the sponsor API (ADFS client id → studies).</summary>
public class SponsorClient
{
    public int Id { get; set; }
    public string ClientId { get; set; } = "";
    public string Name { get; set; } = "";

    /// <summary>Comma separated study codes.</summary>
    public string Studies { get; set; } = "";

    public bool MayUnblind { get; set; }
    public bool Active { get; set; } = true;

    public bool Allows(string studyCode) =>
        Studies.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Contains(studyCode, StringComparer.OrdinalIgnoreCase);
}
