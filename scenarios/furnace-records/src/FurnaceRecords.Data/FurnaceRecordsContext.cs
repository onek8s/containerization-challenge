using FurnaceRecords.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FurnaceRecords.Data;

public class FurnaceRecordsContext : DbContext
{
    public FurnaceRecordsContext(DbContextOptions<FurnaceRecordsContext> options)
        : base(options)
    {
    }

    public DbSet<Furnace> Furnaces => Set<Furnace>();
    public DbSet<FurnaceChannel> Channels => Set<FurnaceChannel>();
    public DbSet<ChannelCorrection> Corrections => Set<ChannelCorrection>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<Load> Loads => Set<Load>();
    public DbSet<LoadPart> LoadParts => Set<LoadPart>();
    public DbSet<LoadSample> Samples => Set<LoadSample>();
    public DbSet<LoadEventRecord> Events => Set<LoadEventRecord>();
    public DbSet<ApiClient> ApiClients => Set<ApiClient>();
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();
    public DbSet<JobRun> JobRuns => Set<JobRun>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasDefaultSchema(Database.IsSqlServer() ? "fr" : null);

        b.Entity<Furnace>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(6);
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Instrumentation).HasConversion<string>().HasMaxLength(1);
        });

        b.Entity<FurnaceChannel>(e =>
        {
            e.HasIndex(x => new { x.FurnaceId, x.Channel }).IsUnique();
            e.Property(x => x.Name).HasMaxLength(50);
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(2);
            e.Property(x => x.SensorSerial).HasMaxLength(40);
            e.HasMany(x => x.Corrections).WithOne().HasForeignKey(x => x.FurnaceChannelId);
        });

        b.Entity<Recipe>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(20);
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Specification).HasMaxLength(100);
        });

        b.Entity<Load>(e =>
        {
            e.HasIndex(x => x.LoadNumber).IsUnique();
            e.HasIndex(x => new { x.FurnaceId, x.Status });
            e.Property(x => x.LoadNumber).HasMaxLength(20);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(12);
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(12);
            e.Property(x => x.CertificatePath).HasMaxLength(260);
            e.Property(x => x.PlannedBy).HasMaxLength(100);
            e.Property(x => x.DecidedBy).HasMaxLength(100);
            e.Property(x => x.DecisionComment).HasMaxLength(1000);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasMany(x => x.Parts).WithOne().HasForeignKey(x => x.LoadId);
        });

        b.Entity<LoadPart>(e =>
        {
            e.Property(x => x.WorkOrder).HasMaxLength(20);
            e.Property(x => x.PartNumber).HasMaxLength(40);
            e.Property(x => x.Lot).HasMaxLength(40);
        });

        b.Entity<LoadSample>(e =>
        {
            e.HasIndex(x => new { x.LoadId, x.AtUtc, x.Channel });
        });

        b.Entity<LoadEventRecord>(e =>
        {
            e.HasIndex(x => new { x.LoadId, x.AtUtc });
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Detail).HasMaxLength(200);
        });

        b.Entity<OutboxMessage>(e =>
        {
            e.HasIndex(x => new { x.SentAtUtc, x.NextAttemptUtc });
            e.Property(x => x.Topic).HasMaxLength(40);
            e.Property(x => x.Key).HasMaxLength(40);
            e.Property(x => x.LastError).HasMaxLength(1000);
        });

        b.Entity<JobRun>(e =>
        {
            e.HasKey(x => x.Job);
            e.Property(x => x.Job).HasMaxLength(40);
            e.Property(x => x.Result).HasMaxLength(400);
        });

        b.Entity<ApiClient>(e =>
        {
            e.HasIndex(x => x.KeyHash).IsUnique();
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.KeyHash).HasMaxLength(64);
        });

        if (!Database.IsSqlServer())
        {
            // SQLite has no rowversion; the column is maintained by SaveChanges.
            b.Entity<Load>().Property(x => x.RowVersion).IsRowVersion().ValueGeneratedNever();
        }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // All times are stored as UTC.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    private sealed class UtcDateTimeConverter : Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime, DateTime>
    {
        public UtcDateTimeConverter()
            : base(v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(), v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
        {
        }
    }

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
        if (Database.IsSqlServer())
        {
            return;
        }
        foreach (var entry in ChangeTracker.Entries<Load>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Entity.RowVersion = Guid.NewGuid().ToByteArray();
            }
        }
    }
}
