using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Runtime.Caching;
using Dapper;
using Tailspin.DealerOrders.Configurator;
using Tailspin.DealerOrders.Domain;
using Tailspin.DealerOrders.Processing.Ports;

namespace Tailspin.DealerOrders.Data.Stores
{
    /// <summary>
    /// The cache schema: prices, stock, exchange rates and items copied from AX. Full replacements are bulk copied into
    /// a staging table and swapped in one transaction.
    /// </summary>
    public class SqlCacheStore : ICacheStore
    {
        private readonly SqlConnectionFactory _connections;

        public SqlCacheStore(SqlConnectionFactory connections)
        {
            _connections = connections;
        }

        public void ReplacePrices(IList<PriceEntry> prices)
        {
            var table = new DataTable();
            table.Columns.Add("ItemId", typeof(string));
            table.Columns.Add("PriceGroup", typeof(string));
            table.Columns.Add("Currency", typeof(string));
            table.Columns.Add("Amount", typeof(decimal));
            table.Columns.Add("QuantityFrom", typeof(decimal));
            table.Columns.Add("FromDate", typeof(DateTime));
            table.Columns.Add("ToDate", typeof(DateTime));
            foreach (var p in prices) table.Rows.Add(p.ItemId, p.PriceGroup, p.Currency, p.Amount, p.QuantityFrom, p.FromDate, (object)p.ToDate ?? DBNull.Value);
            Swap("cache.Price", table);
        }

        public void ReplaceStock(IList<StockLevel> stock)
        {
            var table = new DataTable();
            table.Columns.Add("ItemId", typeof(string));
            table.Columns.Add("ConfigurationId", typeof(string));
            table.Columns.Add("Warehouse", typeof(string));
            table.Columns.Add("PhysicalOnHand", typeof(decimal));
            table.Columns.Add("Reserved", typeof(decimal));
            table.Columns.Add("OnOrder", typeof(decimal));
            table.Columns.Add("NextReceiptDate", typeof(DateTime));
            table.Columns.Add("UpdatedUtc", typeof(DateTime));
            foreach (var s in stock)
                table.Rows.Add(s.ItemId, s.ConfigurationId ?? "", s.Warehouse, s.PhysicalOnHand, s.Reserved, s.OnOrder, (object)s.NextReceiptDate ?? DBNull.Value, s.UpdatedUtc);
            Swap("cache.Stock", table);
        }

        private void Swap(string target, DataTable rows)
        {
            using (var c = _connections.Open())
            using (var tx = c.BeginTransaction())
            {
                var staging = "#" + target.Replace("cache.", "") + "Staging";
                c.Execute($"SELECT TOP (0) * INTO {staging} FROM {target};", transaction: tx);
                using (var bulk = new SqlBulkCopy(c, SqlBulkCopyOptions.TableLock, tx) { DestinationTableName = staging, BulkCopyTimeout = 300 })
                {
                    foreach (DataColumn column in rows.Columns) bulk.ColumnMappings.Add(column.ColumnName, column.ColumnName);
                    bulk.WriteToServer(rows);
                }
                c.Execute($"DELETE FROM {target}; INSERT {target} SELECT * FROM {staging}; DROP TABLE {staging};", transaction: tx, commandTimeout: 300);
                tx.Commit();
            }
            MemoryCache.Default.Remove(SqlPriceSource.CacheKey);
        }

        public void UpsertExchangeRates(IList<ExchangeRate> rates)
        {
            using (var c = _connections.Open())
                c.Execute(@"
MERGE cache.ExchangeRate AS t
USING (SELECT @FromCurrency AS FromCurrency, @ToCurrency AS ToCurrency, @ValidFrom AS ValidFrom, @Rate AS Rate) AS s
   ON t.FromCurrency = s.FromCurrency AND t.ToCurrency = s.ToCurrency AND t.ValidFrom = s.ValidFrom
WHEN MATCHED AND t.Rate <> s.Rate THEN UPDATE SET Rate = s.Rate
WHEN NOT MATCHED THEN INSERT (FromCurrency, ToCurrency, ValidFrom, Rate) VALUES (s.FromCurrency, s.ToCurrency, s.ValidFrom, s.Rate);", rates);
        }

        public void UpsertItems(IList<CachedItem> items)
        {
            using (var c = _connections.Open())
                c.Execute(@"
MERGE cache.Item AS t
USING (SELECT @ItemId AS ItemId, @Name AS Name, @ItemGroup AS ItemGroup, @NetWeightKg AS NetWeightKg, @Stopped AS Stopped) AS s ON t.ItemId = s.ItemId
WHEN MATCHED THEN UPDATE SET Name = s.Name, ItemGroup = s.ItemGroup, NetWeightKg = s.NetWeightKg, Stopped = s.Stopped
WHEN NOT MATCHED THEN INSERT (ItemId, Name, ItemGroup, NetWeightKg, Stopped) VALUES (s.ItemId, s.Name, s.ItemGroup, s.NetWeightKg, s.Stopped);", items);
        }

        public IList<StockLevel> Stock(string itemId, string configurationId)
        {
            using (var c = _connections.Open())
                return c.Query<StockLevel>(@"
SELECT ItemId, ConfigurationId, Warehouse, PhysicalOnHand, Reserved, OnOrder, NextReceiptDate, UpdatedUtc
FROM cache.Stock WHERE ItemId = @itemId AND (@configurationId IS NULL OR ConfigurationId = @configurationId)
ORDER BY Warehouse", new { itemId, configurationId }).ToList();
        }

        public DateTime? LastRun(string job)
        {
            using (var c = _connections.Open())
                return c.ExecuteScalar<DateTime?>("SELECT LastRunUtc FROM cache.SyncRun WHERE Job = @job", new { job });
        }

        public void SetLastRun(string job, DateTime utc, int records)
        {
            using (var c = _connections.Open())
                c.Execute(@"
UPDATE cache.SyncRun SET LastRunUtc = @utc, Records = @records WHERE Job = @job;
IF @@ROWCOUNT = 0 INSERT cache.SyncRun (Job, LastRunUtc, Records) VALUES (@job, @utc, @records);", new { job, utc, records });
        }

        public IList<SyncRunInfo> Runs()
        {
            using (var c = _connections.Open())
                return c.Query<SyncRunInfo>("SELECT Job, LastRunUtc, Records FROM cache.SyncRun ORDER BY Job").ToList();
        }

        public string ItemName(string itemId)
        {
            using (var c = _connections.Open())
                return c.ExecuteScalar<string>("SELECT Name FROM cache.Item WHERE ItemId = @itemId AND Stopped = 0 AND ItemGroup = 'PARTS'", new { itemId });
        }
    }

    public class SyncRunInfo
    {
        public string Job { get; set; }
        public DateTime LastRunUtc { get; set; }
        public int Records { get; set; }
    }

    /// <summary>
    /// Price source for the configurator, read from the cache tables and kept in memory for five minutes (prices change
    /// at most nightly, and the configurator page prices on every click).
    /// </summary>
    public class SqlPriceSource : IPriceSource
    {
        internal const string CacheKey = "dealerorders:prices";
        private readonly SqlConnectionFactory _connections;

        public SqlPriceSource(SqlConnectionFactory connections)
        {
            _connections = connections;
        }

        private PriceData Data
        {
            get
            {
                if (MemoryCache.Default.Get(CacheKey) is PriceData cached) return cached;
                using (var c = _connections.Open())
                using (var multi = c.QueryMultiple(@"
SELECT ItemId, PriceGroup, Currency, Amount, QuantityFrom, FromDate, ToDate FROM cache.Price;
SELECT Tier, DiscountGroup, [Percent] FROM dlr.DiscountAgreement;
SELECT Code, Name, ModelCode, OptionCode, CountryCode, [Percent], FromDate, ToDate FROM dlr.Campaign;
SELECT CountryCode, Zone, Family, AmountEur FROM dlr.FreightRate;
SELECT FromCurrency, ToCurrency, Rate, ValidFrom FROM cache.ExchangeRate;"))
                {
                    var data = new PriceData
                    {
                        Prices = multi.Read<PriceEntry>().ToLookup(p => p.ItemId, StringComparer.OrdinalIgnoreCase),
                        Discounts = multi.Read<DiscountEntry>().ToList(),
                        Campaigns = multi.Read<Campaign>().ToList(),
                        Freight = multi.Read<FreightRate>().ToList(),
                        Rates = multi.Read<ExchangeRate>().ToList()
                    };
                    MemoryCache.Default.Set(CacheKey, data, DateTimeOffset.UtcNow.AddMinutes(5));
                    return data;
                }
            }
        }

        public IEnumerable<PriceEntry> PricesFor(string itemId) => Data.Prices[itemId];
        public IEnumerable<DiscountEntry> Discounts() => Data.Discounts;
        public IEnumerable<Campaign> Campaigns(DateTime date) => Data.Campaigns.Where(c => c.FromDate <= date && c.ToDate >= date);
        public IEnumerable<FreightRate> FreightRates(string countryCode) => Data.Freight.Where(f => f.CountryCode == countryCode);
        public IEnumerable<ExchangeRate> ExchangeRates() => Data.Rates;

        private class PriceData
        {
            public ILookup<string, PriceEntry> Prices { get; set; }
            public List<DiscountEntry> Discounts { get; set; }
            public List<Campaign> Campaigns { get; set; }
            public List<FreightRate> Freight { get; set; }
            public List<ExchangeRate> Rates { get; set; }
        }
    }
}
