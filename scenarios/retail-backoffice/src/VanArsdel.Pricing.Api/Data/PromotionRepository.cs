using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using VanArsdel.Pricing.Api.Domain;

namespace VanArsdel.Pricing.Api.Data
{
    public class PromotionRepository
    {
        private const string Columns = "Id, Code, Name, Type, Channel, Markets, StartsUtc, EndsUtc, Priority, Exclusive, RequiresCode, Value, BuyQuantity, PayQuantity, MinBasket, Status, CreatedUtc";
        private readonly IDbConnectionFactory _db;

        public PromotionRepository(IDbConnectionFactory db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<Promotion>> ListAsync(bool currentOnly)
        {
            using var c = _db.Open();
            var sql = $"SELECT {Columns} FROM Promotion";
            if (currentOnly) sql += " WHERE Status = 'Active' AND EndsUtc > @Now";
            sql += " ORDER BY StartsUtc DESC";
            var promotions = (await c.QueryAsync<Promotion>(sql, new { Now = DateTime.UtcNow })).ToList();
            await LoadScopesAsync(c, promotions);
            return promotions;
        }

        /// <summary>Active promotions for pricing (cached by the caller).</summary>
        public async Task<IReadOnlyList<Promotion>> ActiveAsync(DateTime nowUtc)
        {
            using var c = _db.Open();
            var promotions = (await c.QueryAsync<Promotion>($"SELECT {Columns} FROM Promotion WHERE Status = 'Active' AND StartsUtc <= @Now AND EndsUtc > @Now",
                new { Now = nowUtc })).ToList();
            await LoadScopesAsync(c, promotions);
            return promotions;
        }

        public async Task<Promotion> GetAsync(string code)
        {
            using var c = _db.Open();
            var p = await c.QuerySingleOrDefaultAsync<Promotion>($"SELECT {Columns} FROM Promotion WHERE Code = @Code", new { Code = code });
            if (p != null) await LoadScopesAsync(c, new List<Promotion> { p });
            return p;
        }

        public async Task<int> SaveAsync(Promotion p)
        {
            using var c = _db.Open();
            using var tx = c.BeginTransaction();
            var existingId = await c.ExecuteScalarAsync<int?>("SELECT Id FROM Promotion WHERE Code = @Code", new { p.Code }, tx);
            if (existingId.HasValue)
            {
                p.Id = existingId.Value;
                await c.ExecuteAsync(@"UPDATE Promotion SET Name = @Name, Type = @Type, Channel = @Channel, Markets = @Markets, StartsUtc = @StartsUtc, EndsUtc = @EndsUtc,
                    Priority = @Priority, Exclusive = @Exclusive, RequiresCode = @RequiresCode, Value = @Value, BuyQuantity = @BuyQuantity, PayQuantity = @PayQuantity,
                    MinBasket = @MinBasket WHERE Id = @Id", p, tx);
                await c.ExecuteAsync("DELETE FROM PromotionScope WHERE PromotionId = @Id", new { p.Id }, tx);
            }
            else
            {
                var sql = @"INSERT INTO Promotion (Code, Name, Type, Channel, Markets, StartsUtc, EndsUtc, Priority, Exclusive, RequiresCode, Value, BuyQuantity, PayQuantity, MinBasket, Status, CreatedUtc)
                            VALUES (@Code, @Name, @Type, @Channel, @Markets, @StartsUtc, @EndsUtc, @Priority, @Exclusive, @RequiresCode, @Value, @BuyQuantity, @PayQuantity, @MinBasket, @Status, @CreatedUtc);";
                sql += _db.IsSqlite ? " SELECT last_insert_rowid();" : " SELECT CAST(SCOPE_IDENTITY() AS INT);";
                p.Id = await c.ExecuteScalarAsync<int>(sql, p, tx);
            }
            await c.ExecuteAsync("INSERT INTO PromotionScope (PromotionId, ScopeType, Value) VALUES (@PromotionId, @ScopeType, @Value)",
                p.Scopes.Select(s => new { PromotionId = p.Id, s.ScopeType, s.Value }), tx);
            tx.Commit();
            return p.Id;
        }

        public async Task<bool> SetStatusAsync(string code, string status)
        {
            using var c = _db.Open();
            return await c.ExecuteAsync("UPDATE Promotion SET Status = @Status WHERE Code = @Code", new { Code = code, Status = status }) == 1;
        }

        private static async Task LoadScopesAsync(System.Data.Common.DbConnection c, List<Promotion> promotions)
        {
            if (promotions.Count == 0) return;
            var scopes = await c.QueryAsync<PromotionScope>("SELECT PromotionId, ScopeType, Value FROM PromotionScope WHERE PromotionId IN @Ids",
                new { Ids = promotions.Select(p => p.Id).ToList() });
            var byId = scopes.ToLookup(s => s.PromotionId);
            foreach (var p in promotions) p.Scopes = byId[p.Id].ToList();
        }
    }
}
