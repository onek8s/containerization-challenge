using System;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace VanArsdel.BuildingBlocks.Outbox
{
    public interface IOutbox
    {
        /// <summary>Adds the message to the current unit of work; it is sent after SaveChanges commits.</summary>
        void Add(object message);
    }

    public class EfOutbox<TContext> : IOutbox where TContext : DbContext, IOutboxContext
    {
        private readonly TContext _db;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public EfOutbox(TContext db, IHttpContextAccessor httpContextAccessor = null)
        {
            _db = db;
            _httpContextAccessor = httpContextAccessor;
        }

        public void Add(object message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            _db.OutboxMessages.Add(new OutboxMessage
            {
                MessageId = Guid.NewGuid(),
                MessageType = message.GetType().AssemblyQualifiedName,
                Body = JsonConvert.SerializeObject(message),
                CorrelationId = _httpContextAccessor?.HttpContext?.TraceIdentifier,
                CreatedUtc = DateTime.UtcNow,
            });
        }
    }
}
