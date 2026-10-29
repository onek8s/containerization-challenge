using System;
using System.Linq;
using Wingtip.Invoices.Core.Parsing;
using Xunit;

namespace Wingtip.Invoices.Core.Tests
{
    public class ValidatorTests
    {
        private static readonly DateTime Today = new DateTime(2026, 10, 29);

        [Theory]
        [InlineData("NF_INVOIC_9100231.edi")]
        [InlineData("BP-2026-0815.xml")]
        [InlineData("LOS_77810.csv")]
        public void Samples_are_valid(string name)
        {
            var text = Samples.Text(name);
            var inv = InvoiceParsers.For(InvoiceParsers.Detect(name, text)).Parse(text).Single();
            Assert.Empty(new InvoiceValidator().Validate(inv, Today));
        }

        [Fact]
        public void Totals_and_vat_are_checked()
        {
            var inv = Invoice();
            inv.VatAmount = 30m;
            inv.GrossAmount = 130m;
            inv.Lines[0].NetAmount = 101m;

            var codes = new InvoiceValidator().Validate(inv, Today).Select(i => i.Code).ToList();

            Assert.Contains(IssueCodes.LineTotal, codes);
            Assert.Contains(IssueCodes.HeaderTotal, codes);
            Assert.Contains(IssueCodes.VatTotal, codes);
        }

        [Fact]
        public void Vat_is_rounded_per_rate_not_per_line()
        {
            var inv = new InvoiceDocument { InvoiceNumber = "1", InvoiceDate = Today, SupplierVatNumber = "FI1" };
            for (var i = 1; i <= 3; i++)
                inv.Lines.Add(new InvoiceLine { LineNumber = i, Quantity = 1, UnitPrice = 0.10m, NetAmount = 0.10m, VatRate = 25.5m });
            inv.NetAmount = 0.30m;
            inv.VatAmount = 0.08m;
            inv.GrossAmount = 0.38m;

            Assert.Empty(new InvoiceValidator { RoundingTolerance = 0m }.Validate(inv, Today));
        }

        [Fact]
        public void Dates_and_rates_are_checked()
        {
            var inv = Invoice();
            inv.InvoiceDate = Today.AddDays(5);
            inv.Lines[0].VatRate = 22m;
            var codes = new InvoiceValidator().Validate(inv, Today).Select(i => i.Code).ToList();
            Assert.Contains(IssueCodes.FutureDate, codes);
            Assert.Contains(IssueCodes.VatRate, codes);

            inv.InvoiceDate = Today.AddYears(-2);
            Assert.Contains(IssueCodes.OldInvoice, new InvoiceValidator().Validate(inv, Today).Select(i => i.Code));
        }

        [Fact]
        public void Missing_fields()
        {
            var codes = new InvoiceValidator().Validate(new InvoiceDocument(), Today).Select(i => i.Code).ToList();
            Assert.Equal(3, codes.Count(c => c == IssueCodes.MissingField));
            Assert.Contains(IssueCodes.NoLines, codes);
        }

        internal static InvoiceDocument Invoice()
        {
            var inv = new InvoiceDocument
            {
                InvoiceNumber = "100",
                InvoiceDate = Today,
                SupplierVatNumber = "FI12345671",
                PurchaseOrderNumber = "4500012001",
                NetAmount = 100m,
                VatAmount = 24m,
                GrossAmount = 124m
            };
            inv.Lines.Add(new InvoiceLine { LineNumber = 1, ItemNumber = "100-2040", Quantity = 1000, UnitPrice = 0.1m, NetAmount = 100m, VatRate = 24m });
            return inv;
        }
    }
}
