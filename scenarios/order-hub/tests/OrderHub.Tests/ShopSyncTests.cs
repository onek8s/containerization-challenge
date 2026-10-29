using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Litware.OrderHub.Contracts;
using Litware.OrderHub.Core;
using Litware.OrderHub.Core.Channels;
using Litware.OrderHub.Integration.Consumers;
using Litware.OrderHub.Integration.Mail;
using Litware.OrderHub.Integration.Webshop;
using Litware.OrderHub.Tests.Fakes;
using MassTransit;
using Xunit;

namespace Litware.OrderHub.Tests
{
    public sealed class ShopSyncTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "orderhub-shop-" + Guid.NewGuid().ToString("N"));
        private readonly FakeWebshop _shop = new FakeWebshop();
        private readonly FakeSmtpServer _smtp = new FakeSmtpServer();
        private readonly WebshopApiClient _api;
        private readonly CustomerMailer _mailer;

        public ShopSyncTests()
        {
            _api = new WebshopApiClient(_shop.BaseAddress, "ck_test", "cs_test", TimeSpan.FromSeconds(10));
            _mailer = new CustomerMailer(Path.Combine(AppContext.BaseDirectory, "Templates"),
                new MailSettings { Host = "127.0.0.1", Port = _smtp.Port, From = "Litware Outdoor <noreply@litware-outdoor.example>" });
        }

        [Fact]
        public void Shipment_mail_is_rendered_in_the_customer_language()
        {
            var mail = _mailer.Render("shipped", CustomerMailer.LanguageFor("FI"), "aino@example.com", new
            {
                name = "Aino", orderNumber = "10482", carrier = "POSTI", trackingNumber = "JJFI1",
                trackingUrl = CarrierLinks.TrackingUrl("POSTI", "JJFI1"),
                lines = new[] { new OrderLineDocument { Sku = "LW-HDL-300", Description = "Headlamp 300 lm", Quantity = 2 } }
            });
            Assert.Equal("Litware-tilauksesi 10482 on matkalla", mail.Subject);
            Assert.Contains("posti.fi/en/tracking#/lahetys/JJFI1", mail.HtmlBody);
            Assert.Contains("2 x", mail.HtmlBody);
            Assert.Equal("en", CustomerMailer.LanguageFor("DE"));
        }

        [Fact]
        public async Task Webshop_shipment_completes_the_order_and_mails_the_customer()
        {
            var store = InMemoryOrderStore.WithProducts();
            var doc = WebshopOrderMapper.Map(WebshopOrderMapper.Parse(Samples.Text("webshop-order-10482.json")));
            var order = new OrderRecord { OrderId = Guid.NewGuid(), Channel = SalesChannels.Webshop, ExternalRef = "10482", Status = OrderStatus.Accepted, Document = doc };
            await store.InsertAsync(order);

            var consumer = new ShopShipmentConsumer(store, _api, _mailer, null, "/confirmations");
            var bus = Bus.Factory.CreateUsingInMemory(cfg => cfg.ReceiveEndpoint("shop", e => e.Consumer(() => consumer)));
            await bus.StartAsync();
            try
            {
                await bus.Publish<OrderShipped>(new
                {
                    order.OrderId, order.OrderNumber, order.Channel, order.ExternalRef, Carrier = "POSTI", TrackingNumber = "JJFI1",
                    ShippedUtc = DateTime.UtcNow, Lines = new[] { new ShippedLine { Sku = "LW-TNT-2P-GRN", Quantity = 1 } }
                });

                var until = DateTime.UtcNow.AddSeconds(15);
                while (_smtp.Messages.IsEmpty && DateTime.UtcNow < until) await Task.Delay(100);
            }
            finally
            {
                await bus.StopAsync();
            }

            var put = _shop.Requests.Single(r => r.Method == "PUT");
            Assert.Equal("/wp-json/wc/v3/orders/10482", put.Path);
            Assert.Contains("\"status\":\"completed\"", put.Body);
            Assert.Contains("JJFI1", put.Body);
            Assert.Single(_smtp.Messages);
            Assert.Contains("aino.virtanen@example.com", _smtp.Messages.Single());
            Assert.Contains(store.Events[order.OrderId], e => e.EventType == "ShipmentMailSent");
        }

        [Fact]
        public async Task Stock_sync_pushes_only_known_skus_and_archives_old_snapshots()
        {
            var wms = new FolderSftpConnector(Path.Combine(_root, "wms"));
            var marketplace = new FolderSftpConnector(Path.Combine(_root, "mp"));
            wms.Put("/out/stock/STOCK_202610291630.csv", Encoding.UTF8.GetBytes("Sku,OnHand,Allocated\nLW-HDL-300,1,0\n"));
            wms.Put("/out/stock/STOCK_202610291700.csv", Samples.Bytes("STOCK_202610291700.csv"));
            _shop.ProductsJson = "[{\"id\":311,\"parent_id\":0,\"sku\":\"LW-TNT-2P-GRN\",\"manage_stock\":true}," +
                                 "{\"id\":402,\"parent_id\":400,\"sku\":\"LW-SB-M5-REG\",\"manage_stock\":true}," +
                                 "{\"id\":500,\"parent_id\":0,\"sku\":\"LW-HDL-300\",\"manage_stock\":false}]";

            var job = new StockSyncJob(wms, "/out/stock", _api, marketplace, "/stock", 2);
            Assert.Equal(2, await job.RunAsync(CancellationToken.None));

            var batch = _shop.Requests.Single(r => r.Path == "/wp-json/wc/v3/products/batch");
            Assert.Contains("\"id\":311,\"stock_quantity\":37", batch.Body);
            var variations = _shop.Requests.Single(r => r.Path == "/wp-json/wc/v3/products/400/variations/batch");
            Assert.Contains("\"stock_quantity\":63", variations.Body);
            Assert.Equal(2, Directory.GetFiles(wms.Local("/out/stock/archive")).Length);
            Assert.Contains("LW-STV-GAS;0", File.ReadAllText(marketplace.Local("/stock/offers_stock.csv")));

            // Nothing changed: no more API writes for the next identical snapshot.
            wms.Put("/out/stock/STOCK_202610291730.csv", Samples.Bytes("STOCK_202610291700.csv"));
            int before = _shop.Requests.Count(r => r.Method == "POST");
            Assert.Equal(0, await job.RunAsync(CancellationToken.None));
            Assert.Equal(before, _shop.Requests.Count(r => r.Method == "POST"));
        }

        public void Dispose()
        {
            _api.Dispose();
            _shop.Dispose();
            _smtp.Dispose();
            try { Directory.Delete(_root, true); } catch (IOException) { }
        }
    }
}
