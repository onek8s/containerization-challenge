using Microsoft.EntityFrameworkCore;
using VanArsdel.BuildingBlocks.Outbox;
using VanArsdel.Returns.Api.Domain;

namespace VanArsdel.Returns.Api.Data;

public class ReturnsDbContext : DbContext, IOutboxContext
{
    public ReturnsDbContext(DbContextOptions<ReturnsDbContext> options) : base(options)
    {
    }

    public DbSet<ReturnRequest> Returns => Set<ReturnRequest>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ReturnRequest>(b =>
        {
            b.ToTable("ReturnRequest");
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.RmaNo).IsUnique();
            b.HasIndex(x => x.OrderNo);
            b.HasIndex(x => x.ReturnTrackingNo);
            b.Property(x => x.RmaNo).HasMaxLength(20);
            b.Property(x => x.OrderNo).HasMaxLength(30);
            b.Property(x => x.Channel).HasMaxLength(10);
            b.Property(x => x.StoreCode).HasMaxLength(10);
            b.Property(x => x.CustomerEmail).HasMaxLength(200);
            b.Property(x => x.LoyaltyCardNo).HasMaxLength(20);
            b.Property(x => x.Market).HasMaxLength(2);
            b.Property(x => x.Currency).HasMaxLength(3);
            b.Property(x => x.Status).HasMaxLength(20);
            b.Property(x => x.PaymentReference).HasMaxLength(100);
            b.Property(x => x.PaymentMethod).HasMaxLength(30);
            b.Property(x => x.RefundMethod).HasMaxLength(20);
            b.Property(x => x.RefundReference).HasMaxLength(100);
            b.Property(x => x.ReturnTrackingNo).HasMaxLength(60);
            b.Property(x => x.ReturnLabelUrl).HasMaxLength(300);
            b.Property(x => x.ReceivedAt).HasMaxLength(10);
            b.Property(x => x.RejectReason).HasMaxLength(300);
            b.Property(x => x.HandledBy).HasMaxLength(100);
            b.Property(x => x.RefundAmount).HasColumnType("decimal(18,2)");
            b.Ignore(x => x.LinesValue);
            b.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.ReturnId);
        });
        modelBuilder.Entity<ReturnLine>(b =>
        {
            b.ToTable("ReturnLine");
            b.Property(x => x.Sku).HasMaxLength(40);
            b.Property(x => x.ReasonCode).HasMaxLength(20);
            b.Property(x => x.Grade).HasMaxLength(1);
            b.Property(x => x.UnitPrice).HasColumnType("decimal(18,2)");
        });
        modelBuilder.AddOutbox();
    }
}
