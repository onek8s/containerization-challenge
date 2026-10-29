using System;
using System.Diagnostics;
using log4net;
using Quartz;
using Tailspin.DealerOrders.Data.Stores;
using Tailspin.DealerOrders.Domain;
using Tailspin.DealerOrders.Infrastructure;
using Tailspin.DealerOrders.Processing.Sales;
using Tailspin.DealerOrders.Processing.Sync;

namespace Tailspin.DealerOrders.PriceSync.Jobs
{
    /// <summary>Base for the scheduled jobs: timing, logging and Quartz error handling.</summary>
    [DisallowConcurrentExecution]
    public abstract class SyncJob : IJob
    {
        protected readonly ILog Log;

        protected SyncJob()
        {
            Log = LogManager.GetLogger(GetType());
        }

        public abstract int Run();

        public void Execute(IJobExecutionContext context)
        {
            var watch = Stopwatch.StartNew();
            try
            {
                var count = Run();
                Log.InfoFormat("{0} finished in {1:0.0} s: {2} records", GetType().Name, watch.Elapsed.TotalSeconds, count);
            }
            catch (Exception ex)
            {
                Log.Error(GetType().Name + " failed", ex);
                // Quartz retries at the next trigger; do not refire immediately when AX is down.
                throw new JobExecutionException(ex, refireImmediately: false);
            }
        }
    }

    public class PriceListJob : SyncJob
    {
        private readonly CacheSync _sync;
        public PriceListJob(CacheSync sync) { _sync = sync; }
        public override int Run() => _sync.SyncPrices();
    }

    public class StockJob : SyncJob
    {
        private readonly CacheSync _sync;
        public StockJob(CacheSync sync) { _sync = sync; }
        public override int Run() => _sync.SyncStock();
    }

    public class DealerBalanceJob : SyncJob
    {
        private readonly CacheSync _sync;
        public DealerBalanceJob(CacheSync sync) { _sync = sync; }
        public override int Run() => _sync.SyncDealerBalances();
    }

    public class ExchangeRateJob : SyncJob
    {
        private readonly CacheSync _sync;
        public ExchangeRateJob(CacheSync sync) { _sync = sync; }
        public override int Run() => _sync.SyncExchangeRates();
    }

    public class ItemJob : SyncJob
    {
        private readonly CacheSync _sync;
        public ItemJob(CacheSync sync) { _sync = sync; }
        public override int Run() => _sync.SyncItems();
    }

    public class QuoteExpiryJob : SyncJob
    {
        private readonly QuoteService _quotes;
        public QuoteExpiryJob(QuoteService quotes) { _quotes = quotes; }
        public override int Run() => _quotes.ExpireQuotes();
    }

    public class DailyDigestJob : SyncJob
    {
        private readonly DigestBuilder _digest;
        public DailyDigestJob(DigestBuilder digest) { _digest = digest; }
        public override int Run() => _digest.Run();
    }

    /// <summary>Price lists and order exports are regenerated on demand; old copies are removed after 90 days.</summary>
    public class DocumentPurgeJob : SyncJob
    {
        private readonly SqlDocumentStore _documents;
        public DocumentPurgeJob(SqlDocumentStore documents) { _documents = documents; }

        public override int Run()
        {
            var days = Settings.Int("Documents.PurgeAfterDays", 90);
            var before = DateTime.UtcNow.AddDays(-days);
            return _documents.Purge(DocumentKind.PriceList, before) + _documents.Purge(DocumentKind.OrderExport, before);
        }
    }
}
