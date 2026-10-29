using System;
using System.IO;
using System.Linq;
using System.Text;
using Wingtip.Invoices.Core.Erp;
using Xunit;

namespace Wingtip.Invoices.Core.Tests
{
    public class ErpFileTests
    {
        private static PostingInvoice Posting()
        {
            var p = new PostingInvoice
            {
                InvoiceId = 1042,
                Company = "WT01",
                VendorNumber = "V10021",
                InvoiceNumber = "9100231",
                DocumentType = 380,
                InvoiceDate = new DateTime(2026, 10, 20),
                DueDate = new DateTime(2026, 11, 19),
                Currency = "EUR",
                NetAmount = 218.00m,
                VatAmount = 55.59m,
                GrossAmount = 273.59m,
                PurchaseOrderNumber = "4500012001"
            };
            p.Lines.Add(new PostingLine { LineNumber = 1, Account = "14100", CostCenter = "WH01", Quantity = 2000, NetAmount = 170m, VatRate = 25.5m, PurchaseOrderNumber = "4500012001", PurchaseOrderLine = 1 });
            p.Lines.Add(new PostingLine { LineNumber = 2, Account = "14100", CostCenter = "WH01", Quantity = 1500, NetAmount = 48m, VatRate = 25.5m, PurchaseOrderNumber = "4500012001", PurchaseOrderLine = 2 });
            return p;
        }

        [Fact]
        public void Records_have_fixed_layout()
        {
            var records = ErpImportFile.Format(7, "WT01", new DateTime(2026, 10, 29, 14, 30, 0), new[] { Posting() });

            Assert.Equal(5, records.Count);
            Assert.All(records, r => Assert.Equal(ErpImportFile.RecordLength, r.Length));
            Assert.StartsWith("APH0000000007WT0120261029143000000001", records[0]);
            Assert.StartsWith("API0000001042V10021    9100231             3802026102020261119EUR00000000027359+00000000005559+", records[1]);
            Assert.StartsWith("APL0000001042000114100   WH01  00000000017000+S54500012001000100002000000+", records[2]);
            Assert.StartsWith("APT000000050000000000027359+", records[4]);
        }

        [Fact]
        public void Credit_amounts_have_trailing_minus()
        {
            var p = Posting();
            p.DocumentType = 381;
            p.NetAmount = -218m; p.VatAmount = -55.59m; p.GrossAmount = -273.59m;
            foreach (var l in p.Lines) { l.NetAmount = -l.NetAmount; l.Quantity = -l.Quantity; }

            var records = ErpImportFile.Format(1, "WT01", DateTime.Now, new[] { p });
            Assert.Contains("00000000027359-", records[1]);
            Assert.Contains("S54500012001000100002000000-", records[2]);
        }

        [Fact]
        public void Unbalanced_invoice_is_refused()
        {
            var p = Posting();
            p.Lines[0].NetAmount = 171m;
            Assert.Throws<InvalidOperationException>(() => ErpImportFile.Format(1, "WT01", DateTime.Now, new[] { p }));
        }

        [Fact]
        public void File_is_written_in_erp_code_page()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var p = Posting();
            p.PaymentReference = "Ä-1";
            using (var ms = new MemoryStream())
            {
                ErpImportFile.Write(ms, Encoding.GetEncoding(850), 1, "WT01", DateTime.Now, new[] { p });
                var bytes = ms.ToArray();
                Assert.Equal(5 * (ErpImportFile.RecordLength + 2), bytes.Length);
                Assert.Contains((byte)0x8E, bytes); // Ä in code page 850
            }
        }

        [Fact]
        public void Vat_codes()
        {
            Assert.Equal("S5", ErpImportFile.VatCode(25.5m));
            Assert.Equal("S1", ErpImportFile.VatCode(24m));
            Assert.Equal("Z0", ErpImportFile.VatCode(0m));
            Assert.Throws<ArgumentOutOfRangeException>(() => ErpImportFile.VatCode(19m));
        }

        [Fact]
        public void Acknowledgements_are_read()
        {
            var acks = ErpAcknowledgement.Parse(new StringReader("APS;2;1;1\r\nAPA;1042;OK;AP2026-118833;\r\nAPA;1043;ER;;Vendor V10045 blocked; contact purchasing\r\n"));
            Assert.Equal(2, acks.Count);
            Assert.True(acks[0].Accepted);
            Assert.Equal("AP2026-118833", acks[0].VoucherNumber);
            Assert.False(acks[1].Accepted);
            Assert.Equal("Vendor V10045 blocked; contact purchasing", acks[1].Message);
            Assert.Throws<FormatException>(() => ErpAcknowledgement.Parse(new StringReader("XYZ")));
        }

        [Theory]
        [InlineData("2026-10-20", "N30", null, "2026-11-19")]
        [InlineData("2026-10-20", "EOM30", null, "2026-11-30")]
        [InlineData("2026-10-20", "N14", "2026-11-30", "2026-11-30")]
        [InlineData("2026-10-20", "N60", "2026-11-01", "2026-12-19")]
        [InlineData("2026-10-20", null, null, "2026-11-19")]
        public void Due_dates(string invoiceDate, string terms, string due, string expected)
        {
            var result = PaymentTerms.DueDate(DateTime.Parse(invoiceDate), terms, due == null ? (DateTime?)null : DateTime.Parse(due));
            Assert.Equal(DateTime.Parse(expected), result);
        }

        [Theory]
        [InlineData("FI 2345678-2", "FI23456782")]
        [InlineData("2345678-2", "FI23456782")]
        [InlineData("ee100234567", "EE100234567")]
        public void Vat_numbers_are_normalised(string input, string expected)
        {
            Assert.Equal(expected, VatNumber.Normalize(input));
        }
    }
}
