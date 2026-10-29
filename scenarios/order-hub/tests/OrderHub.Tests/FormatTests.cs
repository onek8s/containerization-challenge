using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Litware.OrderHub.Contracts;
using Litware.OrderHub.Core;
using Litware.OrderHub.Core.Channels;
using Litware.OrderHub.Core.Partner;
using Litware.OrderHub.Core.Warehouse;
using Xunit;

namespace Litware.OrderHub.Tests
{
    public class FormatTests
    {
        [Fact]
        public void Webhook_signature_round_trips_and_rejects_tampering()
        {
            byte[] body = Samples.Bytes("webshop-order-10482.json");
            string signature = WebhookSignature.Compute(body, "s3cr3t");

            Assert.True(WebhookSignature.IsValid(body, signature, "s3cr3t"));
            Assert.False(WebhookSignature.IsValid(body, signature, "other"));
            body[10] ^= 1;
            Assert.False(WebhookSignature.IsValid(body, signature, "s3cr3t"));
            Assert.False(WebhookSignature.IsValid(body, null, "s3cr3t"));
        }

        [Fact]
        public void Webshop_order_maps_to_hub_document()
        {
            var order = WebshopOrderMapper.Parse(Samples.Text("webshop-order-10482.json"));
            var doc = WebshopOrderMapper.Map(order);

            Assert.True(WebshopOrderMapper.IsFulfillable(order));
            Assert.Equal("Aino Virtanen", doc.CustomerName);
            Assert.Equal("FI", doc.ShipTo.CountryCode);
            Assert.Equal("10482", doc.CustomerReference);
            Assert.Equal(6.90m, doc.ShippingAmount);
            Assert.Equal(2, doc.Lines.Count);
            Assert.Equal(66.90m, doc.Lines[1].UnitPrice);
            Assert.Null(doc.ShipTo.Street2);
        }

        [Fact]
        public void Marketplace_file_groups_lines_and_reads_decimal_commas()
        {
            using (var stream = File.OpenRead(Samples.Path("trailmarket_orders_20261029_1400.csv")))
            {
                var orders = MarketplaceOrderFile.Read(stream);

                Assert.Equal(2, orders.Count);
                Assert.Equal("TM-7781204", orders[0].OrderRef);
                Assert.Equal(2, orders[0].Document.Lines.Count);
                Assert.Equal(29.90m, orders[0].Document.Lines[1].UnitPrice);
                Assert.Equal(4.90m, orders[0].Document.ShippingAmount);
                Assert.Equal("Hansen; Mette", orders[1].Document.ShipTo.Name);
                Assert.Equal("København N", orders[1].Document.ShipTo.City);
            }
        }

        [Fact]
        public void Marketplace_file_with_missing_column_is_rejected()
        {
            var ex = Assert.Throws<FormatException>(() => MarketplaceOrderFile.Read(new StringReader("OrderRef;Sku\nA;B\n")));
            Assert.Contains("OrderDate", ex.Message);
        }

        [Fact]
        public void Partner_order_maps_po_and_store()
        {
            var doc = PartnerOrder.Parse(Samples.Text("contoso-po-4500123456.json")).ToDocument();
            Assert.Equal("4500123456/ST-0142", doc.CustomerReference);
            Assert.Equal("dropship", doc.ShippingMethod);
            Assert.Equal("4500123456", DispatchNotice.PoFromCustomerReference(doc.CustomerReference));
            Assert.Equal(119.20m, doc.Lines[0].UnitPrice);
        }

        [Fact]
        public void Warehouse_order_file_has_lines_and_consignee()
        {
            var order = new OrderRecord
            {
                OrderNumber = "LW1000042",
                Channel = SalesChannels.Webshop,
                Document = WebshopOrderMapper.Map(WebshopOrderMapper.Parse(Samples.Text("webshop-order-10482.json")))
            };
            var xml = XDocument.Parse(Encoding.UTF8.GetString(WarehouseOrderFile.ToBytes(WarehouseOrderFile.Build(order, "LITWARE"))));
            var ns = WarehouseOrderFile.Ns;

            Assert.Equal("ORD_LW1000042.xml", WarehouseOrderFile.FileName(order));
            Assert.Equal("LITWARE", xml.Root.Attribute("client").Value);
            Assert.Equal("STD", xml.Root.Element(ns + "Service").Value);
            Assert.Equal("00100", xml.Root.Element(ns + "Consignee").Element(ns + "Zip").Value);
            Assert.Equal(new[] { "1", "2" }, xml.Root.Element(ns + "Lines").Elements().Select(e => e.Attribute("no").Value));
            Assert.Null(xml.Root.Element(ns + "Consignee").Element(ns + "Address2"));
        }

        [Fact]
        public void Shipment_confirmation_groups_by_tracking_number_and_converts_to_utc()
        {
            var shipments = ShipmentConfirmationFile.Read(new StringReader(Samples.Text("SHIPCONF_202610291630.csv")));
            Assert.Equal(2, shipments.Count);
            Assert.Equal("LW1000001", shipments[0].OrderNumber);
            Assert.Equal(2, shipments[0].Lines.Count);
            Assert.Equal(new DateTime(2026, 10, 29, 14, 22, 41, DateTimeKind.Utc), shipments[0].ShippedUtc);
            Assert.Equal(DateTimeKind.Utc, shipments[0].ShippedUtc.Kind);
        }

        [Fact]
        public void Stock_report_sums_locations_and_subtracts_allocations()
        {
            var levels = StockReportFile.Read(new StringReader(Samples.Text("STOCK_202610291700.csv"))).ToDictionary(l => l.Sku);
            Assert.Equal(65, levels["LW-SB-M5-REG"].Available);
            Assert.Equal(188, levels["LW-HDL-300"].Available);
            Assert.Equal(0, levels["LW-STV-GAS"].Available);
            Assert.Equal(new DateTime(2026, 10, 29, 17, 0, 0), StockReportFile.SnapshotTime("/out/stock/STOCK_202610291700.csv"));
            Assert.Null(StockReportFile.SnapshotTime("stock.csv"));
        }

        [Fact]
        public void Marketplace_shipment_file_has_bom_and_header()
        {
            var bytes = MarketplaceShipmentFile.Build("TM-1", "DHL", "0034", new DateTime(2026, 10, 29, 14, 0, 0, DateTimeKind.Utc),
                new[] { new ShippedLine { Sku = "LW-HDL-300", Quantity = 2 } });
            Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3));
            var text = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
            Assert.Equal(MarketplaceShipmentFile.Header + "\r\nTM-1;LW-HDL-300;2;DHL;0034;2026-10-29T14:00:00Z\r\n", text);
        }

        [Fact]
        public void Dispatch_notice_json_uses_partner_names()
        {
            var json = new DispatchNotice
            {
                PoNumber = "4500123456", SupplierOrderNo = "LW1000003", InvoiceNo = "INV0500001", Carrier = "POSTI",
                TrackingNo = "JJFI1", ShippedAtUtc = new DateTime(2026, 10, 29, 14, 0, 0, DateTimeKind.Utc),
                Lines = { new DispatchLine { ArticleNo = "LW-PK-65-BLK", Qty = 1 } }
            }.ToJson();
            Assert.Contains("\"shippedAt\":\"2026-10-29T14:00:00Z\"", json);
            Assert.Contains("\"articleNo\":\"LW-PK-65-BLK\"", json);
        }

        [Fact]
        public void Carrier_links_are_built_for_known_carriers()
        {
            Assert.Equal("https://www.posti.fi/en/tracking#/lahetys/JJFI1", CarrierLinks.TrackingUrl("Posti", "JJFI1"));
            Assert.Null(CarrierLinks.TrackingUrl("Unknown", "1"));
        }

        [Fact]
        public void Csv_quoting_round_trips()
        {
            string quoted = CsvLine.Quote("a;\"b\"", ';');
            Assert.Equal("\"a;\"\"b\"\"\"", quoted);
            Assert.Equal(new[] { "x", "a;\"b\"", "" }, CsvLine.Split("x;" + quoted + ";", ';'));
        }
    }
}
