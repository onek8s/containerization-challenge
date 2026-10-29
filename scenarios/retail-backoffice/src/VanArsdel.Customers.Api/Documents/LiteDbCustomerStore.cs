using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LiteDB;

namespace VanArsdel.Customers.Api.Documents
{
    /// <summary>Single-file document store for test environments.</summary>
    public sealed class LiteDbCustomerStore : ICustomerStore, IDisposable
    {
        private readonly LiteDatabase _db;
        private readonly ILiteCollection<CustomerDocument> _customers;
        private readonly object _gate = new object();

        public LiteDbCustomerStore(string path)
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            _db = new LiteDatabase(new ConnectionString { Filename = path, Connection = ConnectionType.Shared });
            _customers = _db.GetCollection<CustomerDocument>("customers");
            _customers.EnsureIndex(c => c.Email, true);
            _customers.EnsureIndex(c => c.Loyalty.CardNo);
            _customers.EnsureIndex(c => c.LastName);
        }

        public Task<CustomerDocument> GetAsync(string id, CancellationToken ct) => Task.FromResult(_customers.FindById(id));

        public Task<CustomerDocument> FindByEmailAsync(string email, CancellationToken ct) => Task.FromResult(_customers.FindOne(c => c.Email == email));

        public Task<CustomerDocument> FindByCardAsync(string cardNo, CancellationToken ct) => Task.FromResult(_customers.FindOne(c => c.Loyalty.CardNo == cardNo));

        public Task<IReadOnlyList<CustomerDocument>> SearchAsync(string text, int take, CancellationToken ct)
        {
            var t = (text ?? string.Empty).ToLowerInvariant();
            IReadOnlyList<CustomerDocument> result = _customers.Find(c => c.Email.StartsWith(t) || c.LastName.StartsWith(text) || c.Phone.StartsWith(text) || c.Loyalty.CardNo.StartsWith(text))
                .Take(take).ToList();
            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<CustomerDocument>> PageAsync(int skip, int take, CancellationToken ct)
        {
            IReadOnlyList<CustomerDocument> result = _customers.Query().OrderBy(c => c.Id).Skip(skip).Limit(take).ToList();
            return Task.FromResult(result);
        }

        public Task<long> CountAsync(CancellationToken ct) => Task.FromResult(_customers.LongCount());

        public Task<bool> SaveAsync(CustomerDocument document, CancellationToken ct)
        {
            lock (_gate)
            {
                var current = _customers.FindById(document.Id);
                if ((current?.Version ?? 0) != document.Version) return Task.FromResult(false);
                if (document.Version == 0 && document.Email != null && _customers.Exists(c => c.Email == document.Email)) return Task.FromResult(false);
                document.Version++;
                _customers.Upsert(document);
                return Task.FromResult(true);
            }
        }

        public Task<bool> PingAsync(CancellationToken ct) => Task.FromResult(true);

        public void Dispose() => _db.Dispose();
    }
}
