using System;
using System.IO;
using Xunit;

namespace Contoso.Purchasing.Core.Tests
{
    public class ErpOrderExportTests
    {
        [Fact]
        public void Writes_header_lines_and_trailer()
        {
            var request = TestData.Request(4.2m, 100);
            request.Title = "Gloves; nitrile";
            request.Lines.Add(new RequestLine { LineNo = 2, Description = "Delivery", Quantity = 1, UnitPrice = 15m, VatRate = 25.5m });

            var order = new PurchaseOrder
            {
                PoNumber = "4500000017",
                BuyerAccount = @"CONTOSO\tniemi",
                OrderDate = new DateTime(2026, 3, 10),
                Request = request,
                Supplier = new Supplier { ErpVendorNo = "V10044" }
            };

            var writer = new StringWriter();
            var export = new ErpOrderExport();
            export.Write(writer, new[] { order });

            var expected =
                "H;4500000017;V10044;20260310;4100;EUR;tniemi;PR-2026-00007 Gloves, nitrile\r\n" +
                "L;4500000017;10;Item;100;PCS;4.20;25.5\r\n" +
                "L;4500000017;20;Delivery;1;PCS;15.00;25.5\r\n" +
                "T;1;2;435.00\r\n";
            Assert.Equal(expected, writer.ToString());
            Assert.Equal("PURCH01_20260310_220000.csv", export.FileName(new DateTime(2026, 3, 10, 22, 0, 0)));
        }
    }
}
