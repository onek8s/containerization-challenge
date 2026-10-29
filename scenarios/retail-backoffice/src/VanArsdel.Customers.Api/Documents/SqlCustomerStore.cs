using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using VanArsdel.Customers.Api.Data;

namespace VanArsdel.Customers.Api.Documents
{
    /// <summary>
    /// Documents as JSON in a SQL table, with the lookup fields as columns. Used where no MongoDB server
    /// is available.
    /// </summary>
    public class SqlCustomerStore : ICustomerStore
    {
        private readonly CustomersDbContext _db;

        public SqlCustomerStore(CustomersDbContext db)
        {
            _db = db;
        }

        public async Task<CustomerDocument> GetAsync(string id, CancellationToken ct) => Map(await _db.Documents.AsNoTracking().SingleOrDefaultAsync(d => d.Id == id, ct));

        public async Task<CustomerDocument> FindByEmailAsync(string email, CancellationToken ct) => Map(await _db.Documents.AsNoTracking().SingleOrDefaultAsync(d => d.Email == email, ct));

        public async Task<CustomerDocument> FindByCardAsync(string cardNo, CancellationToken ct) => Map(await _db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.CardNo == cardNo, ct));

        public async Task<IReadOnlyList<CustomerDocument>> SearchAsync(string text, int take, CancellationToken ct)
        {
            var rows = await _db.Documents.AsNoTracking()
                .Where(d => d.Email.StartsWith(text.ToLower()) || d.LastName.StartsWith(text) || d.Phone.StartsWith(text) || d.CardNo.StartsWith(text))
                .Take(take).ToListAsync(ct);
            return rows.Select(Map).ToList();
        }

        public async Task<IReadOnlyList<CustomerDocument>> PageAsync(int skip, int take, CancellationToken ct)
        {
            var rows = await _db.Documents.AsNoTracking().OrderBy(d => d.Id).Skip(skip).Take(take).ToListAsync(ct);
            return rows.Select(Map).ToList();
        }

        public Task<long> CountAsync(CancellationToken ct) => _db.Documents.LongCountAsync(ct);

        public async Task<bool> SaveAsync(CustomerDocument document, CancellationToken ct)
        {
            var expected = document.Version;
            var row = await _db.Documents.SingleOrDefaultAsync(d => d.Id == document.Id, ct);
            if ((row?.Version ?? 0) != expected) return false;
            if (row == null)
            {
                if (document.Email != null && await _db.Documents.AnyAsync(d => d.Email == document.Email, ct)) return false;
                row = new CustomerDocumentRow { Id = document.Id };
                _db.Documents.Add(row);
            }
            document.Version = expected + 1;
            row.Version = document.Version;
            row.Email = document.Email;
            row.LastName = document.LastName;
            row.Phone = document.Phone;
            row.CardNo = document.Loyalty?.CardNo;
            row.Json = JsonConvert.SerializeObject(document);
            row.UpdatedUtc = DateTime.UtcNow;
            try
            {
                await _db.SaveChangesAsync(ct);
                return true;
            }
            catch (DbUpdateException)
            {
                document.Version = expected;
                _db.ChangeTracker.Clear();
                return false;
            }
        }

        public async Task<bool> PingAsync(CancellationToken ct) => await _db.Database.CanConnectAsync(ct);

        private static CustomerDocument Map(CustomerDocumentRow row) => row == null ? null : JsonConvert.DeserializeObject<CustomerDocument>(row.Json);
    }
}
