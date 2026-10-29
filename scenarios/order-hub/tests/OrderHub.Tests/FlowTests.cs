using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Litware.OrderHub.Contracts;
using Litware.OrderHub.Core;
using Litware.OrderHub.Core.Channels;
using Litware.OrderHub.Core.Partner;
using Litware.OrderHub.Core.Warehouse;
using Litware.OrderHub.Integration.Configuration;
using Litware.OrderHub.Integration.Consumers;
using Litware.OrderHub.Integration.Messaging;
using Litware.OrderHub.Integration.Partners;
using Litware.OrderHub.Integration.Reporting;
using Litware.OrderHub.Tests.Fakes;
using MassTransit;
using Xunit;

namespace Litware.OrderHub.Tests
{
    /// <summary>
    /// Runs the fulfilment and partner consumers together: on the in-memory transport by default, or on a
    /// RabbitMQ broker when ORDERHUB_TEST_RABBITMQ is set (rabbitmq://host/vhost;user;password).
    /// </summary>
    public sealed class FlowTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "orderhub-flow-" + Guid.NewGuid().ToString("N"));
        private readonly InMemoryOrderStore _store = InMemoryOrderStore.WithProducts();
        private readonly FolderSftpConnector _warehouse;
        private readonly FolderSftpConnector _marketplace;
        private readonly FolderPartnerGateway _partner;
        private readonly IBusControl _bus;

        public FlowTests()
        {
            _warehouse = new FolderSftpConnector(Path.Combine(_root, "wms"));
            _marketplace = new FolderSftpConnector(Path.Combine(_root, "marketplace"));
            _partner = new FolderPartnerGateway(Path.Combine(_root, "partner", "in"), Path.Combine(_root, "partner", "out"), TimeSpan.FromMilliseconds(200));

            var validator = new OrderValidator(new[] { "FI", "SE", "DE", "DK" }, new[] { "EUR", "SEK" });
            var renderer = new FakeRenderer();
            string suffix = Guid.NewGuid().ToString("N").Substring(0, 6);

            void Endpoints(Action<string, Action<IReceiveEndpointConfigurator>> endpoint)
            {
                endpoint(HubBus.Queues.FulfilmentOrders + "." + suffix, e => e.Consumer(() => new OrderIntakeConsumer(_store, validator)));
                endpoint(HubBus.Queues.FulfilmentWarehouse + "." + suffix, e => e.Consumer(() => new WarehouseOrderConsumer(_store, _warehouse, "/in", "LITWARE")));
                endpoint(HubBus.Queues.PartnersInvoicing + "." + suffix, e => e.Consumer(() => new InvoiceConsumer(_store, renderer)));
                endpoint(HubBus.Queues.PartnersDispatch + "." + suffix, e => e.Consumer(() => new PartnerDispatchConsumer(_store, _partner)));
            }

            string rabbit = Environment.GetEnvironmentVariable("ORDERHUB_TEST_RABBITMQ");
            if (string.IsNullOrEmpty(rabbit))
            {
                _bus = Bus.Factory.CreateUsingInMemory(cfg => Endpoints((q, c) => cfg.ReceiveEndpoint(q, e => { c(e); })));
            }
            else
            {
                var parts = rabbit.Split(';');
                var settings = new BusSettings { HostAddress = new Uri(parts[0]), Username = parts[1], Password = parts[2] };
                _bus = HubBus.Create(settings, (cfg, host) => Endpoints((q, c) => cfg.ReceiveEndpoint(host, q, e =>
                {
                    e.AutoDelete = true;
                    e.Durable = false;
                    c(e);
                })));
            }
            _bus.Start();
        }

        private static async Task Until(Func<bool> condition, string what, int seconds = 20)
        {
            var until = DateTime.UtcNow.AddSeconds(seconds);
            while (!condition())
            {
                if (DateTime.UtcNow > until) throw new TimeoutException("Timed out waiting for: " + what);
                await Task.Delay(100);
            }
        }

        private Task Submit(string channel, string externalRef, OrderDocument doc)
        {
            return _bus.Publish<OrderSubmitted>(new
            {
                OrderId = NewId.NextGuid(), Channel = channel, ExternalRef = externalRef, SubmittedUtc = DateTime.UtcNow, Order = doc
            });
        }

        [Fact]
        public async Task Webshop_order_goes_to_warehouse_ships_and_is_invoiced()
        {
            var doc = WebshopOrderMapper.Map(WebshopOrderMapper.Parse(Samples.Text("webshop-order-10482.json")));
            await Submit(SalesChannels.Webshop, "10482", doc);

            string wmsFile = Path.Combine(_warehouse.Root, "in", "ORD_LW1000001.xml");
            await Until(() => File.Exists(wmsFile), "warehouse order file");
            await Until(() => _store.Orders.Single().Status == OrderStatus.SentToWarehouse, "SentToWarehouse");
            var xml = XDocument.Load(wmsFile);
            Assert.Equal("LW1000001", xml.Root.Element(WarehouseOrderFile.Ns + "OrderNo").Value);

            // Duplicate webhook delivery is ignored.
            await Submit(SalesChannels.Webshop, "10482", doc);
            await Until(() => _store.GetEventsAsync(_store.Orders[0].OrderId).Result.Any(e => e.EventType == "Duplicate"), "duplicate event");
            Assert.Single(_store.Orders);

            _warehouse.Put("/out/SHIPCONF_202610291630.csv", Samples.Bytes("SHIPCONF_202610291630.csv"));
            var poller = new ShipmentConfirmationPoller(_store, _warehouse, _bus, "/out");
            Assert.Equal(1, await poller.PollAsync());

            await Until(() => _store.GetEventsAsync(_store.Orders[0].OrderId).Result.Any(e => e.EventType == "Invoiced"), "Invoiced event");
            var order = _store.Orders[0];
            Assert.Equal("POSTI", order.Carrier);
            Assert.Equal("INV0500001", order.InvoiceNumber);
            Assert.Equal("%PDF-fake LW1000001", Encoding.ASCII.GetString(_store.Invoices["INV0500001"]));
            Assert.True(File.Exists(Path.Combine(_warehouse.Root, "out", "archive", "SHIPCONF_202610291630.csv")));
            Assert.Empty(Directory.GetFiles(PartnerOut()));
        }

        private string PartnerOut() => Path.Combine(_root, "partner", "out");

        [Fact]
        public async Task Partner_order_gets_dispatch_notice_after_invoicing()
        {
            var receiver = new PartnerOrderReceiver(_bus);
            File.Copy(Samples.Path("contoso-po-4500123456.json"), Path.Combine(_root, "partner", "in", "4500123456.json"));
            _partner.StartReceiving(receiver.HandleAsync);

            await Until(() => _store.Orders.Count == 1 && _store.Orders[0].Status == OrderStatus.SentToWarehouse, "partner order at warehouse");
            var order = _store.Orders[0];
            Assert.Equal(SalesChannels.Partner, order.Channel);

            string conf = "OrderNo,LineNo,Sku,QtyShipped,Carrier,TrackingNo,ShipDate\n" +
                          order.OrderNumber + ",1,LW-PK-65-BLK,1,POSTI,JJFI9,2026-10-30T09:00:00+02:00\n" +
                          order.OrderNumber + ",2,LW-SB-M5-LNG,1,POSTI,JJFI9,2026-10-30T09:00:00+02:00\n";
            _warehouse.Put("/out/SHIPCONF_202610300900.csv", Encoding.UTF8.GetBytes(conf));
            await new ShipmentConfirmationPoller(_store, _warehouse, _bus, "/out").PollAsync();

            string notice = Path.Combine(PartnerOut(), order.OrderNumber + ".json");
            await Until(() => File.Exists(notice), "dispatch notice");
            var json = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(notice));
            Assert.Equal("4500123456", (string)json["poNumber"]);
            Assert.Equal("JJFI9", (string)json["trackingNo"]);
            Assert.Equal("INV0500001", (string)json["invoiceNo"]);
            Assert.True(File.Exists(Path.Combine(_root, "partner", "in", "done", "4500123456.json")));
        }

        [Fact]
        public async Task Rejected_order_can_be_resubmitted_after_fixing_master_data()
        {
            var doc = WebshopOrderMapper.Map(WebshopOrderMapper.Parse(Samples.Text("webshop-order-10483.json")));
            await Submit(SalesChannels.Webshop, "10483", doc);
            await Until(() => _store.Orders.Count == 1 && _store.Orders[0].Status == OrderStatus.Rejected, "rejected");
            Assert.Contains("discontinued", _store.Orders[0].StatusReason);
            Assert.Null(_store.Orders[0].OrderNumber);

            _store.Products["LW-PK-40-RED"].Active = true;
            await _bus.Publish<ResubmitOrder>(new { _store.Orders[0].OrderId, RequestedBy = "test" });
            await Until(() => _store.Orders[0].Status == OrderStatus.SentToWarehouse, "resubmitted order at warehouse");
            Assert.Equal("LW1000001", _store.Orders[0].OrderNumber);
        }

        [Fact]
        public async Task Marketplace_poller_publishes_each_order_and_moves_the_file()
        {
            _marketplace.Put("/orders/trailmarket_orders_20261029_1400.csv", Samples.Bytes("trailmarket_orders_20261029_1400.csv"));
            _marketplace.Put("/orders/broken.csv", Encoding.UTF8.GetBytes("OrderRef;Sku\nX;Y\n"));
            var poller = new MarketplaceOrderPoller(_marketplace, _bus, "/orders");

            Assert.Equal(2, await poller.PollAsync());
            await Until(() => _store.Orders.Count(o => o.Status == OrderStatus.SentToWarehouse) == 2, "two marketplace orders at warehouse");
            Assert.True(File.Exists(Path.Combine(_marketplace.Root, "orders", "processed", "trailmarket_orders_20261029_1400.csv")));
            Assert.True(File.Exists(Path.Combine(_marketplace.Root, "orders", "rejected", "broken.csv")));
        }

        [Fact]
        public async Task Daily_sales_workbook_is_created_once()
        {
            var doc = WebshopOrderMapper.Map(WebshopOrderMapper.Parse(Samples.Text("webshop-order-10482.json")));
            await Submit(SalesChannels.Webshop, "10482", doc);
            await Until(() => _store.Orders.Count == 1 && _store.Orders[0].OrderNumber != null, "accepted");
            await _store.SaveInvoiceAsync(_store.Orders[0].OrderId, null);

            DateTime tomorrow = DateTime.Now.Date.AddDays(1).AddHours(3);
            var job = new DailySalesReportJob(_store, new TimeSpan(2, 0, 0), () => tomorrow);
            Assert.Equal(SalesWorkbook.FileName(DateTime.Now.Date), await job.RunIfDueAsync());
            Assert.Null(await job.RunIfDueAsync());

            using (var package = new OfficeOpenXml.ExcelPackage(new MemoryStream(_store.Reports.Values.Single().Content)))
            {
                var sheet = package.Workbook.Worksheets["Invoice lines"];
                Assert.Equal("INV0500001", sheet.Cells[2, 1].Value);
                Assert.Equal("LW-TNT-2P-GRN", sheet.Cells[2, 7].Value);
                Assert.Equal("SUM(K2:K3)", sheet.Cells[4, 11].Formula);
                Assert.Equal(3, package.Workbook.Worksheets.Count);
            }
        }

        public void Dispose()
        {
            _partner.StopAsync().GetAwaiter().GetResult();
            _bus.Stop(TimeSpan.FromSeconds(10));
            try { Directory.Delete(_root, true); } catch (IOException) { }
        }

        private class FakeRenderer : IInvoiceRenderer
        {
            public Task<byte[]> RenderAsync(string orderNumber, CancellationToken cancellationToken) =>
                Task.FromResult(Encoding.ASCII.GetBytes("%PDF-fake " + orderNumber));
        }
    }
}
