using System;
using System.Collections.Generic;
using System.Linq;
using log4net;
using Tailspin.DealerOrders.Contracts.Ax;
using Tailspin.DealerOrders.Domain;
using Tailspin.DealerOrders.Processing.Ports;

namespace Tailspin.DealerOrders.Processing.Sync
{
    /// <summary>
    /// Copies AX master data into the SQL cache read by the portal: trade agreement prices, on-hand stock, customer
    /// balances and credit status, exchange rates and items. Each run reads everything through the Query Service in pages
    /// and replaces the cached set in one transaction, so the portal never sees a half-loaded price list.
    /// </summary>
    public class CacheSync
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(CacheSync));

        private readonly IAxQueryReader _query;
        private readonly ICacheStore _cache;
        private readonly IDealerStore _dealers;
        private readonly IClock _clock;
        private readonly ProcessingSettings _settings;

        public CacheSync(IAxQueryReader query, ICacheStore cache, IDealerStore dealers, IClock clock, ProcessingSettings settings)
        {
            _query = query;
            _cache = cache;
            _dealers = dealers;
            _clock = clock;
            _settings = settings;
        }

        public int PageSize { get; set; } = 500;

        private AxCallContext Context => new AxCallContext { Company = _settings.AxCompany, Language = _settings.AxLanguage };

        public int SyncPrices()
        {
            var records = ReadAll((pos, ctx) => _query.ReadSalesPrices(pos, PageSize, ctx));
            var prices = records
                .Where(r => r.Amount >= 0 && !string.IsNullOrEmpty(r.ItemRelation))
                .Select(r => new PriceEntry
                {
                    ItemId = r.ItemRelation,
                    PriceGroup = string.IsNullOrEmpty(r.AccountRelation) ? "LIST" : r.AccountRelation,
                    Currency = r.Currency,
                    Amount = r.Amount,
                    QuantityFrom = r.QuantityAmountFrom,
                    FromDate = r.FromDate == DateTime.MinValue ? new DateTime(1900, 1, 1) : r.FromDate,
                    ToDate = r.ToDate
                })
                .ToList();
            if (prices.Count == 0) throw new InvalidOperationException("AX returned no sales prices; the cached price list was kept.");
            _cache.ReplacePrices(prices);
            _cache.SetLastRun("prices", _clock.UtcNow, prices.Count);
            Log.InfoFormat("Price cache replaced: {0} trade agreement lines", prices.Count);
            return prices.Count;
        }

        public int SyncStock()
        {
            var now = _clock.UtcNow;
            var stock = ReadAll((pos, ctx) => _query.ReadAvailability(pos, PageSize, ctx))
                .GroupBy(r => new { r.ItemId, ConfigId = r.ConfigId ?? "", r.InventLocationId })
                .Select(g => new StockLevel
                {
                    ItemId = g.Key.ItemId,
                    ConfigurationId = g.Key.ConfigId,
                    Warehouse = g.Key.InventLocationId,
                    PhysicalOnHand = g.Sum(x => x.PhysicalInvent),
                    Reserved = g.Sum(x => x.ReservPhysical),
                    OnOrder = g.Sum(x => x.OnOrder),
                    NextReceiptDate = g.Min(x => x.NextReceiptDate),
                    UpdatedUtc = now
                })
                .ToList();
            _cache.ReplaceStock(stock);
            _cache.SetLastRun("stock", now, stock.Count);
            Log.InfoFormat("Stock cache replaced: {0} item/warehouse rows", stock.Count);
            return stock.Count;
        }

        public int SyncDealerBalances()
        {
            var balances = ReadAll((pos, ctx) => _query.ReadCustomerBalances(pos, PageSize, ctx)).ToDictionary(b => b.AccountNum, StringComparer.OrdinalIgnoreCase);
            var updated = 0;
            foreach (var dealer in _dealers.All())
            {
                if (!balances.TryGetValue(dealer.AccountNumber, out var balance)) continue;
                var hold = balance.Blocked != AxCustomerBlocked.No;
                if (dealer.OpenBalance == balance.Balance && dealer.CreditLimit == balance.CreditMax && dealer.CreditHold == hold) continue;
                if (dealer.CreditHold != hold) Log.InfoFormat("Dealer {0} credit hold {1}", dealer.AccountNumber, hold ? "set" : "released");
                dealer.OpenBalance = balance.Balance;
                dealer.CreditLimit = balance.CreditMax;
                dealer.CreditHold = hold;
                _dealers.Save(dealer);
                updated++;
            }
            _cache.SetLastRun("balances", _clock.UtcNow, updated);
            return updated;
        }

        public int SyncExchangeRates()
        {
            var rates = ReadAll((pos, ctx) => _query.ReadExchangeRates(pos, PageSize, ctx))
                .Select(r => new ExchangeRate { FromCurrency = r.FromCurrency, ToCurrency = r.ToCurrency, Rate = r.Rate, ValidFrom = r.ValidFrom })
                .ToList();
            _cache.UpsertExchangeRates(rates);
            _cache.SetLastRun("rates", _clock.UtcNow, rates.Count);
            return rates.Count;
        }

        public int SyncItems()
        {
            var items = ReadAll((pos, ctx) => _query.ReadItems(pos, PageSize, ctx))
                .Select(i => new CachedItem { ItemId = i.ItemId, Name = i.Name, ItemGroup = i.ItemGroup, NetWeightKg = i.NetWeightKg, Stopped = i.Stopped })
                .ToList();
            _cache.UpsertItems(items);
            _cache.SetLastRun("items", _clock.UtcNow, items.Count);
            return items.Count;
        }

        private List<T> ReadAll<T>(Func<int, AxCallContext, AxPage<T>> read)
        {
            var all = new List<T>();
            int? position = 1;
            var context = Context;
            var pages = 0;
            while (position.HasValue)
            {
                var page = read(position.Value, context);
                all.AddRange(page.Records);
                position = page.NextPosition;
                if (++pages > 10000) throw new InvalidOperationException("Query Service paging does not end.");
            }
            return all;
        }
    }
}
