using System;
using System.Linq;
using System.Threading.Tasks;
using Litware.OrderHub.Contracts;
using Litware.OrderHub.Core;
using Litware.OrderHub.Core.Channels;
using Litware.OrderHub.Integration.Data;
using Litware.OrderHub.Integration.Reporting;
using Xunit;

namespace Litware.OrderHub.Tests
{
    /// <summary>Runs against a database created with db\*.sql when ORDERHUB_TEST_SQL holds a connection string for orderhub_svc.</summary>
    public class SqlOrderStoreTests
    {
        private static readonly string ConnectionString = Environment.GetEnvironmentVariable("ORDERHUB_TEST_SQL");

        [Fact]
        public async Task Order_lifecycle_round_trips_through_the_database()
        {
            if (string.IsNullOrEmpty(ConnectionString)) return;
            var store = new SqlOrderStore(ConnectionString);

            var doc = WebshopOrderMapper.Map(WebshopOrderMapper.Parse(Samples.Text("webshop-order-10482.json")));
            string externalRef = "T" + Guid.NewGuid().ToString("N").Substring(0, 10);
            var order = new OrderRecord
            {
                OrderId = Guid.NewGuid(), Channel = SalesChannels.Webshop, ExternalRef = externalRef,
                Status = OrderStatus.Accepted, ReceivedUtc = DateTime.UtcNow, Document = doc
            };

            Assert.True(await store.InsertAsync(order));
            Assert.Matches("^LW[0-9]{7}$", order.OrderNumber);

            var duplicate = new OrderRecord { OrderId = Guid.NewGuid(), Channel = order.Channel, ExternalRef = externalRef, Status = OrderStatus.Accepted, ReceivedUtc = DateTime.UtcNow, Document = doc };
            Assert.False(await store.InsertAsync(duplicate));
            Assert.Null(duplicate.OrderNumber);

            var loaded = await store.FindByNumberAsync(order.OrderNumber);
            Assert.Equal(order.OrderId, loaded.OrderId);
            Assert.Equal(2, loaded.Document.Lines.Count);
            Assert.Equal("FI", loaded.Document.ShipTo.CountryCode);
            Assert.Equal("EUR", loaded.Document.Currency);
            Assert.Equal(66.90m, loaded.Document.Lines[1].UnitPrice);

            await store.MarkSentToWarehouseAsync(order.OrderId, "ORD_" + order.OrderNumber + ".xml");
            await store.MarkShippedAsync(order.OrderId, "POSTI", "JJFI1", DateTime.UtcNow,
                new[] { new ShippedLine { Sku = "LW-TNT-2P-GRN", Quantity = 1 }, new ShippedLine { Sku = "LW-MAT-AIR-R", Quantity = 1 } });
            string invoice = await store.SaveInvoiceAsync(order.OrderId, null);
            Assert.Equal(invoice, await store.SaveInvoiceAsync(order.OrderId, new byte[] { 1, 2, 3 }));
            Assert.Equal(new byte[] { 1, 2, 3 }, await store.GetInvoicePdfAsync(invoice));

            loaded = await store.FindAsync(order.OrderId);
            Assert.Equal(OrderStatus.Invoiced, loaded.Status);
            Assert.Equal("JJFI1", loaded.TrackingNumber);

            await store.AddEventAsync(order.OrderId, "Test", "detail");
            Assert.Contains(await store.GetEventsAsync(order.OrderId), e => e.EventType == "Test");

            Assert.Contains(await store.SearchAsync(new OrderQuery { Text = externalRef }), o => o.OrderId == order.OrderId);
            Assert.Contains(await store.SearchAsync(new OrderQuery { Text = "Virtanen", Status = OrderStatus.Invoiced }), o => o.OrderId == order.OrderId);

            var lines = await store.GetInvoicedLinesAsync(DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(1));
            var mine = lines.Where(l => l.InvoiceNumber == invoice).ToList();
            Assert.Equal(2, mine.Count);
            Assert.Equal(66.90m, mine.Single(l => l.Sku == "LW-MAT-AIR-R").LineAmount);

            Assert.NotNull(await store.FindProductAsync("LW-HDL-300"));
            Assert.True((await store.CountByStatusAsync(DateTime.UtcNow.AddHours(-1)))[OrderStatus.Invoiced] >= 1);

            string name = "test-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".xlsx";
            await store.SaveReportFileAsync(name, DateTime.Today, SalesWorkbook.Build(DateTime.Today, mine));
            Assert.NotNull(await store.GetReportFileAsync(name));
        }

        [Fact]
        public async Task Rejected_order_gets_a_number_when_accepted_later()
        {
            if (string.IsNullOrEmpty(ConnectionString)) return;
            var store = new SqlOrderStore(ConnectionString);
            var doc = WebshopOrderMapper.Map(WebshopOrderMapper.Parse(Samples.Text("webshop-order-10483.json")));
            var order = new OrderRecord
            {
                OrderId = Guid.NewGuid(), Channel = SalesChannels.Webshop, ExternalRef = "R" + Guid.NewGuid().ToString("N").Substring(0, 10),
                Status = OrderStatus.Rejected, StatusReason = "SKU LW-PK-40-RED is discontinued.", ReceivedUtc = DateTime.UtcNow, Document = doc
            };
            Assert.True(await store.InsertAsync(order));
            Assert.Null((await store.FindAsync(order.OrderId)).OrderNumber);

            await store.UpdateValidationAsync(order.OrderId, OrderStatus.Accepted, null);
            var loaded = await store.FindAsync(order.OrderId);
            Assert.Equal(OrderStatus.Accepted, loaded.Status);
            Assert.NotNull(loaded.OrderNumber);
            Assert.Null(loaded.StatusReason);
        }
    }
}
