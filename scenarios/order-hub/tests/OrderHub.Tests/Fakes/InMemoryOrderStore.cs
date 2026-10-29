using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Litware.OrderHub.Contracts;
using Litware.OrderHub.Core;
using Litware.OrderHub.Integration.Data;

namespace Litware.OrderHub.Tests.Fakes
{
    internal class InMemoryOrderStore : IOrderStore
    {
        private readonly object _lock = new object();
        private int _nextOrder = 1000001;
        private int _nextInvoice = 500001;

        private readonly List<OrderRecord> _orders = new List<OrderRecord>();

        /// <summary>Snapshot of the stored orders (consumers write from other threads).</summary>
        public List<OrderRecord> Orders { get { lock (_lock) return _orders.ToList(); } }
        public Dictionary<Guid, List<OrderEvent>> Events { get; } = new Dictionary<Guid, List<OrderEvent>>();
        public Dictionary<string, Product> Products { get; } = new Dictionary<string, Product>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, byte[]> Invoices { get; } = new Dictionary<string, byte[]>();
        public Dictionary<string, (DateTime Day, byte[] Content)> Reports { get; } = new Dictionary<string, (DateTime, byte[])>();
        public Dictionary<Guid, DateTime> InvoicedUtc { get; } = new Dictionary<Guid, DateTime>();

        public static InMemoryOrderStore WithProducts()
        {
            var store = new InMemoryOrderStore();
            foreach (var p in new[]
            {
                new Product { Sku = "LW-TNT-2P-GRN", Description = "Fjell 2P tent, green", Active = true },
                new Product { Sku = "LW-SB-M5-REG", Description = "Polar -5 sleeping bag, regular", Active = true },
                new Product { Sku = "LW-SB-M5-LNG", Description = "Polar -5 sleeping bag, long", Active = true },
                new Product { Sku = "LW-MAT-AIR-R", Description = "Airmat regular", Active = true },
                new Product { Sku = "LW-PK-65-BLK", Description = "Trek 65 backpack, black", Active = true },
                new Product { Sku = "LW-STV-GAS", Description = "Gas stove compact", Active = true },
                new Product { Sku = "LW-HDL-300", Description = "Headlamp 300 lm", Active = true },
                new Product { Sku = "LW-PK-40-RED", Description = "Trek 40 backpack, red (2017)", Active = false }
            })
            {
                store.Products[p.Sku] = p;
            }
            return store;
        }

        private OrderRecord Get(Guid id) => _orders.FirstOrDefault(o => o.OrderId == id);

        public Task<OrderRecord> FindAsync(Guid orderId) { lock (_lock) return Task.FromResult(Get(orderId)); }
        public Task<OrderRecord> FindByNumberAsync(string n) { lock (_lock) return Task.FromResult(_orders.FirstOrDefault(o => o.OrderNumber == n)); }
        public Task<OrderRecord> FindByExternalRefAsync(string c, string r) { lock (_lock) return Task.FromResult(_orders.FirstOrDefault(o => o.Channel == c && o.ExternalRef == r)); }

        public Task<bool> InsertAsync(OrderRecord order)
        {
            lock (_lock)
            {
                if (_orders.Any(o => o.Channel == order.Channel && o.ExternalRef == order.ExternalRef)) return Task.FromResult(false);
                if (order.Status == OrderStatus.Accepted) order.OrderNumber = "LW" + (_nextOrder++).ToString("D7");
                order.UpdatedUtc = DateTime.UtcNow;
                _orders.Add(order);
                return Task.FromResult(true);
            }
        }

        public Task UpdateValidationAsync(Guid id, string status, string reason)
        {
            lock (_lock)
            {
                var o = Get(id);
                o.Status = status;
                o.StatusReason = reason;
                if (status == OrderStatus.Accepted && o.OrderNumber == null) o.OrderNumber = "LW" + (_nextOrder++).ToString("D7");
            }
            return Task.CompletedTask;
        }

        public Task MarkSentToWarehouseAsync(Guid id, string fileName)
        {
            lock (_lock)
            {
                var o = Get(id);
                if (o.Status == OrderStatus.Accepted) o.Status = OrderStatus.SentToWarehouse;
                o.WarehouseFile = fileName;
            }
            return Task.CompletedTask;
        }

        public Task MarkShippedAsync(Guid id, string carrier, string tracking, DateTime shippedUtc, IEnumerable<ShippedLine> lines)
        {
            lock (_lock)
            {
                var o = Get(id);
                if (o.Status == OrderStatus.Accepted || o.Status == OrderStatus.SentToWarehouse) o.Status = OrderStatus.Shipped;
                o.Carrier = carrier;
                o.TrackingNumber = tracking;
                o.ShippedUtc = shippedUtc;
            }
            return Task.CompletedTask;
        }

        public Task<string> SaveInvoiceAsync(Guid id, byte[] pdf)
        {
            lock (_lock)
            {
                var o = Get(id);
                if (o.InvoiceNumber == null)
                {
                    o.InvoiceNumber = "INV" + (_nextInvoice++).ToString("D7");
                    o.Status = OrderStatus.Invoiced;
                    InvoicedUtc[id] = DateTime.UtcNow;
                }
                if (pdf != null) Invoices[o.InvoiceNumber] = pdf;
                return Task.FromResult(o.InvoiceNumber);
            }
        }

        public Task AddEventAsync(Guid id, string type, string detail)
        {
            lock (_lock)
            {
                if (!Events.TryGetValue(id, out var list)) Events[id] = list = new List<OrderEvent>();
                list.Add(new OrderEvent { AtUtc = DateTime.UtcNow, EventType = type, Detail = detail });
            }
            return Task.CompletedTask;
        }

        public Task<IList<OrderEvent>> GetEventsAsync(Guid id)
        {
            lock (_lock) return Task.FromResult<IList<OrderEvent>>(Events.TryGetValue(id, out var l) ? l.ToList() : new List<OrderEvent>());
        }

        public Task<Product> FindProductAsync(string sku) => Task.FromResult(Products.TryGetValue(sku, out var p) ? p : null);
        public Task<IList<Product>> GetProductsAsync() => Task.FromResult<IList<Product>>(Products.Values.ToList());
        public Task SetProductActiveAsync(string sku, bool active) { Products[sku].Active = active; return Task.CompletedTask; }

        public Task<IList<OrderRecord>> SearchAsync(OrderQuery q)
        {
            lock (_lock) return Task.FromResult<IList<OrderRecord>>(_orders.Where(o => q.Status == null || o.Status == q.Status).Take(q.Top).ToList());
        }

        public Task<IList<SalesLine>> GetInvoicedLinesAsync(DateTime fromUtc, DateTime toUtc)
        {
            lock (_lock)
            {
                var lines = from o in _orders
                            where o.InvoiceNumber != null && InvoicedUtc[o.OrderId] >= fromUtc && InvoicedUtc[o.OrderId] < toUtc
                            from l in o.Document.Lines
                            select new SalesLine
                            {
                                InvoiceNumber = o.InvoiceNumber, OrderNumber = o.OrderNumber, Channel = o.Channel,
                                InvoicedUtc = InvoicedUtc[o.OrderId], ShipCountry = o.Document.ShipTo.CountryCode,
                                Currency = o.Document.Currency, Sku = l.Sku, Description = l.Description,
                                Quantity = l.Quantity, UnitPrice = l.UnitPrice, LineAmount = l.Quantity * l.UnitPrice
                            };
                return Task.FromResult<IList<SalesLine>>(lines.ToList());
            }
        }

        public Task<IDictionary<string, int>> CountByStatusAsync(DateTime sinceUtc)
        {
            lock (_lock) return Task.FromResult<IDictionary<string, int>>(_orders.GroupBy(o => o.Status).ToDictionary(g => g.Key, g => g.Count()));
        }

        public Task<byte[]> GetInvoicePdfAsync(string n) => Task.FromResult(Invoices.TryGetValue(n, out var b) ? b : null);

        public Task SaveReportFileAsync(string name, DateTime day, byte[] content) { Reports[name] = (day, content); return Task.CompletedTask; }

        public Task<IList<ReportFileInfo>> GetReportFilesAsync(int top) =>
            Task.FromResult<IList<ReportFileInfo>>(Reports.Select(r => new ReportFileInfo { FileName = r.Key, ReportDate = r.Value.Day, Size = r.Value.Content.Length }).ToList());

        public Task<byte[]> GetReportFileAsync(string name) => Task.FromResult(Reports.TryGetValue(name, out var r) ? r.Content : null);
    }
}
