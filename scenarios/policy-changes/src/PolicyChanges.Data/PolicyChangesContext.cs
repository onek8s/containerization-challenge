using Microsoft.EntityFrameworkCore;
using PolicyChanges.Data.Entities;

namespace PolicyChanges.Data;

public class PolicyChangesContext : DbContext
{
    public PolicyChangesContext(DbContextOptions<PolicyChangesContext> options) : base(options) { }

    public DbSet<ChangeRequest> ChangeRequests => Set<ChangeRequest>();
    public DbSet<ChangeEvent> ChangeEvents => Set<ChangeEvent>();
    public DbSet<ChangeEvidence> ChangeEvidence => Set<ChangeEvidence>();
    public DbSet<Partner> Partners => Set<Partner>();
    public DbSet<DeskStation> DeskStations => Set<DeskStation>();

    public bool IsSqlServer => Database.ProviderName == "Microsoft.EntityFrameworkCore.SqlServer";

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<ChangeRequest>(e =>
        {
            e.HasIndex(x => x.Reference).IsUnique();
            e.HasIndex(x => x.PolicyNumber);
            e.HasIndex(x => new { x.Status, x.NextAttemptUtc });
            e.HasIndex(x => new { x.PartnerId, x.PartnerReference });
            e.Property(x => x.Reference).HasMaxLength(20);
            e.Property(x => x.PolicyNumber).HasMaxLength(12);
            e.Property(x => x.ChangeType).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Channel).HasConversion<string>().HasMaxLength(10);
            e.Property(x => x.SubmittedBy).HasMaxLength(100);
            e.Property(x => x.PartnerReference).HasMaxLength(50);
            e.Property(x => x.LockedBy).HasMaxLength(50);
            e.Property(x => x.Endorsement).HasMaxLength(20);
            e.Property(x => x.ResultMessage).HasMaxLength(500);
            e.HasMany(x => x.Events).WithOne().HasForeignKey(x => x.ChangeRequestId);
            e.HasMany(x => x.Evidence).WithOne().HasForeignKey(x => x.ChangeRequestId);
        });

        b.Entity<ChangeEvent>(e =>
        {
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Message).HasMaxLength(500);
            e.Property(x => x.By).HasMaxLength(100);
        });

        b.Entity<ChangeEvidence>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(50);
            e.Property(x => x.RelativePath).HasMaxLength(260);
            e.Property(x => x.Station).HasMaxLength(50);
        });

        b.Entity<Partner>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => x.ApiKeyHash).IsUnique();
            e.Property(x => x.Code).HasMaxLength(20);
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.ApiKeyHash).HasMaxLength(64);
        });

        b.Entity<DeskStation>(e =>
        {
            e.HasKey(x => x.Name);
            e.Property(x => x.Name).HasMaxLength(50);
            e.Property(x => x.UserName).HasMaxLength(100);
            e.Property(x => x.ClientVersion).HasMaxLength(30);
            e.Property(x => x.State).HasMaxLength(30);
            e.Property(x => x.Detail).HasMaxLength(300);
            e.Property(x => x.CurrentReference).HasMaxLength(20);
        });
    }
}
