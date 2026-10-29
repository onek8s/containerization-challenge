using System;
using System.Linq;
using log4net;
using Tailspin.DealerOrders.Contracts.Ax;
using Tailspin.DealerOrders.Domain;
using Tailspin.DealerOrders.Processing.Ports;

namespace Tailspin.DealerOrders.Processing.Ax
{
    /// <summary>
    /// Finds sales orders changed in AX since the last run (Query Service, TSDealerOrderChanges query on
    /// SalesTable.ModifiedDateTime) and reads each changed portal order through AIF. Orders that have not been read for a
    /// day are read anyway so that missed changes (for example a packing slip posted in a closed period) are picked up.
    /// </summary>
    public class ChangedOrderPoller
    {
        private const string JobName = "order-status";
        private static readonly ILog Log = LogManager.GetLogger(typeof(ChangedOrderPoller));

        private readonly IAxQueryReader _query;
        private readonly IAxDocumentReader _reader;
        private readonly IOrderStore _orders;
        private readonly ICacheStore _cache;
        private readonly OrderStatusSync _sync;
        private readonly IClock _clock;
        private readonly ProcessingSettings _settings;

        public ChangedOrderPoller(IAxQueryReader query, IAxDocumentReader reader, IOrderStore orders, ICacheStore cache, OrderStatusSync sync, IClock clock, ProcessingSettings settings)
        {
            _query = query;
            _reader = reader;
            _orders = orders;
            _cache = cache;
            _sync = sync;
            _clock = clock;
            _settings = settings;
        }

        public int Run()
        {
            var context = new AxCallContext { Company = _settings.AxCompany, Language = _settings.AxLanguage };
            var started = _clock.UtcNow;
            // Overlap by five minutes: AOS and SQL clocks drift and ModifiedDateTime is set at commit.
            var since = (_cache.LastRun(JobName) ?? started.AddDays(-7)).AddMinutes(-5);
            var read = 0;
            int? position = 1;
            var seen = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);

            while (position.HasValue)
            {
                var page = _query.ReadChangedOrders(since, position.Value, 200, context);
                foreach (var header in page.Records)
                {
                    if (!seen.Add(header.SalesId)) continue;
                    var order = _orders.FindByAxSalesId(header.SalesId);
                    if (order == null) continue;
                    read += ReadOne(order, context) ? 1 : 0;
                }
                position = page.NextPosition;
            }

            foreach (var order in _orders.OpenInAx().Where(o => !seen.Contains(o.AxSalesId) && (!o.AxLastReadUtc.HasValue || o.AxLastReadUtc.Value < started.AddDays(-1))))
                read += ReadOne(order, context) ? 1 : 0;

            _cache.SetLastRun(JobName, started, read);
            return read;
        }

        private bool ReadOne(SalesOrder order, AxCallContext context)
        {
            try
            {
                var snapshot = _reader.ReadSalesOrder(order.AxSalesId, context);
                var changes = _sync.Apply(order, snapshot);
                if (changes.Count > 0) Log.InfoFormat("Order {0} ({1}): {2}", order.OrderNumber, order.AxSalesId, string.Join(", ", changes));
                return true;
            }
            catch (AxBusinessException ex)
            {
                Log.Warn($"Reading {order.AxSalesId} for order {order.OrderNumber} failed", ex);
                return false;
            }
        }
    }
}
