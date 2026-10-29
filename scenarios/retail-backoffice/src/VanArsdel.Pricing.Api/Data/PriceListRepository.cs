using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;

namespace VanArsdel.Pricing.Api.Data
{
    public class PriceList
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Market { get; set; }
        public string Currency { get; set; }
        public DateTime ValidFrom { get; set; }
        public string Status { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime? PublishedUtc { get; set; }
        public int ItemCount { get; set; }
    }

    public class PriceListItem
    {
        public string Sku { get; set; }
        public decimal Price { get; set; }
        public decimal? CompareAtPrice { get; set; }
    }

    public static class PriceListStatus
    {
        public const string Draft = "Draft";
        public const string Scheduled = "Scheduled";
        public const string Published = "Published";
        public const string Cancelled = "Cancelled";
    }

    public class PriceListRepository
    {
        private readonly IDbConnectionFactory _db;

        public PriceListRepository(IDbConnectionFactory db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<PriceList>> ListAsync(string market, string status)
        {
            using var c = _db.Open();
            var sql = @"SELECT l.Id, l.Name, l.Market, l.Currency, l.ValidFrom, l.Status, l.CreatedBy, l.CreatedUtc, l.PublishedUtc,
                               (SELECT COUNT(*) FROM PriceListItem i WHERE i.PriceListId = l.Id) AS ItemCount
                        FROM PriceList l WHERE 1 = 1";
            if (!string.IsNullOrEmpty(market)) sql += " AND l.Market = @Market";
            if (!string.IsNullOrEmpty(status)) sql += " AND l.Status = @Status";
            sql += " ORDER BY l.ValidFrom DESC, l.Id DESC";
            return (await c.QueryAsync<PriceList>(sql, new { Market = market, Status = status })).ToList();
        }

        public async Task<PriceList> GetAsync(int id)
        {
            using var c = _db.Open();
            return await c.QuerySingleOrDefaultAsync<PriceList>(@"SELECT l.Id, l.Name, l.Market, l.Currency, l.ValidFrom, l.Status, l.CreatedBy, l.CreatedUtc, l.PublishedUtc,
                    (SELECT COUNT(*) FROM PriceListItem i WHERE i.PriceListId = l.Id) AS ItemCount FROM PriceList l WHERE l.Id = @Id", new { Id = id });
        }

        public async Task<IReadOnlyList<PriceListItem>> GetItemsAsync(int id)
        {
            using var c = _db.Open();
            return (await c.QueryAsync<PriceListItem>("SELECT Sku, Price, CompareAtPrice FROM PriceListItem WHERE PriceListId = @Id ORDER BY Sku", new { Id = id })).ToList();
        }

        public async Task<int> CreateAsync(PriceList list, IEnumerable<PriceListItem> items)
        {
            using var c = _db.Open();
            using var tx = c.BeginTransaction();
            var insert = "INSERT INTO PriceList (Name, Market, Currency, ValidFrom, Status, CreatedBy, CreatedUtc) VALUES (@Name, @Market, @Currency, @ValidFrom, @Status, @CreatedBy, @CreatedUtc);";
            insert += _db.IsSqlite ? " SELECT last_insert_rowid();" : " SELECT CAST(SCOPE_IDENTITY() AS INT);";
            var id = await c.ExecuteScalarAsync<int>(insert, list, tx);
            await c.ExecuteAsync("INSERT INTO PriceListItem (PriceListId, Sku, Price, CompareAtPrice) VALUES (@PriceListId, @Sku, @Price, @CompareAtPrice)",
                items.Select(i => new { PriceListId = id, i.Sku, i.Price, i.CompareAtPrice }), tx);
            tx.Commit();
            return id;
        }

        public async Task<bool> SetStatusAsync(int id, string expected, string status)
        {
            using var c = _db.Open();
            var rows = await c.ExecuteAsync("UPDATE PriceList SET Status = @Status WHERE Id = @Id AND Status = @Expected", new { Id = id, Status = status, Expected = expected });
            return rows == 1;
        }

        public async Task<IReadOnlyList<PriceList>> DueAsync(DateTime upTo)
        {
            using var c = _db.Open();
            return (await c.QueryAsync<PriceList>("SELECT Id, Name, Market, Currency, ValidFrom, Status FROM PriceList WHERE Status = @Status AND ValidFrom <= @UpTo ORDER BY ValidFrom, Id",
                new { Status = PriceListStatus.Scheduled, UpTo = upTo.Date })).ToList();
        }

        /// <summary>Copies the list into EffectivePrice and marks it published, in one transaction.</summary>
        public async Task<IReadOnlyList<PriceListItem>> PublishAsync(int id)
        {
            using var c = _db.Open();
            using var tx = c.BeginTransaction();
            var list = await c.QuerySingleAsync<PriceList>("SELECT Id, Market, Currency, ValidFrom, Status FROM PriceList WHERE Id = @Id", new { Id = id }, tx);
            var items = (await c.QueryAsync<PriceListItem>("SELECT Sku, Price, CompareAtPrice FROM PriceListItem WHERE PriceListId = @Id", new { Id = id }, tx)).ToList();
            var upsert = _db.Upsert("EffectivePrice", new[] { "Sku", "Market" }, new[] { "Price", "CompareAtPrice", "PriceListId", "ValidFrom" });
            await c.ExecuteAsync(upsert, items.Select(i => new { i.Sku, list.Market, i.Price, i.CompareAtPrice, PriceListId = id, list.ValidFrom }), tx);
            await c.ExecuteAsync("UPDATE PriceList SET Status = @Status, PublishedUtc = @Now WHERE Id = @Id",
                new { Id = id, Status = PriceListStatus.Published, Now = DateTime.UtcNow }, tx);
            tx.Commit();
            return items;
        }
    }
}
