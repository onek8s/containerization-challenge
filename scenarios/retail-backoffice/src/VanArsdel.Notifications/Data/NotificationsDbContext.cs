using Microsoft.EntityFrameworkCore;

namespace VanArsdel.Notifications.Data;

/// <summary>What was sent to whom. Prevents duplicates when an event is redelivered and answers "did the customer get the e-mail".</summary>
public class SentNotification
{
    public long Id { get; set; }
    public string DedupeKey { get; set; } = "";
    public string Template { get; set; } = "";
    public string Channel { get; set; } = "";
    public string Reference { get; set; } = "";
    public string RecipientHash { get; set; } = "";
    public string Provider { get; set; } = "";
    public string ProviderMessageId { get; set; }
    public string Status { get; set; } = "";
    public string Error { get; set; }
    public DateTime SentUtc { get; set; }
}

public class NotificationsDbContext : DbContext
{
    public NotificationsDbContext(DbContextOptions<NotificationsDbContext> options) : base(options)
    {
    }

    public DbSet<SentNotification> Sent => Set<SentNotification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SentNotification>(b =>
        {
            b.ToTable("SentNotification");
            b.HasIndex(x => x.DedupeKey).IsUnique();
            b.HasIndex(x => x.RecipientHash);
            b.HasIndex(x => x.Reference);
            b.Property(x => x.DedupeKey).HasMaxLength(200);
            b.Property(x => x.Template).HasMaxLength(50);
            b.Property(x => x.Channel).HasMaxLength(10);
            b.Property(x => x.Reference).HasMaxLength(60);
            b.Property(x => x.RecipientHash).HasMaxLength(64);
            b.Property(x => x.Provider).HasMaxLength(20);
            b.Property(x => x.ProviderMessageId).HasMaxLength(100);
            b.Property(x => x.Status).HasMaxLength(10);
            b.Property(x => x.Error).HasMaxLength(1000);
        });
    }
}
