using System;
using Microsoft.EntityFrameworkCore;

namespace VanArsdel.BuildingBlocks.Outbox
{
    /// <summary>
    /// Integration event stored in the same transaction as the business change and published
    /// by <see cref="OutboxDispatcher{TContext}"/>.
    /// </summary>
    public class OutboxMessage
    {
        public long Id { get; set; }
        public Guid MessageId { get; set; }
        public string MessageType { get; set; }
        public string Body { get; set; }
        public string CorrelationId { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime? SentUtc { get; set; }
        public int Attempts { get; set; }
        public string LastError { get; set; }
    }

    public interface IOutboxContext
    {
        DbSet<OutboxMessage> OutboxMessages { get; }
    }

    public static class OutboxModelBuilderExtensions
    {
        public static ModelBuilder AddOutbox(this ModelBuilder modelBuilder, string schema = null)
        {
            modelBuilder.Entity<OutboxMessage>(b =>
            {
                b.ToTable("OutboxMessage", schema);
                b.HasKey(x => x.Id);
                b.Property(x => x.MessageType).HasMaxLength(300).IsRequired();
                b.Property(x => x.Body).IsRequired();
                b.Property(x => x.CorrelationId).HasMaxLength(64);
                b.Property(x => x.LastError).HasMaxLength(2000);
                b.HasIndex(x => x.MessageId).IsUnique();
                b.HasIndex(x => new { x.SentUtc, x.Id });
            });
            return modelBuilder;
        }
    }
}
