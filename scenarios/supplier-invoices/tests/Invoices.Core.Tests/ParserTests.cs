using System;
using System.Linq;
using Wingtip.Invoices.Core.Parsing;
using Xunit;

namespace Wingtip.Invoices.Core.Tests
{
    public class ParserTests
    {
        [Fact]
        public void Edifact_sample_is_parsed()
        {
            var invoices = new EdifactInvoiceParser().Parse(Samples.Text("NF_INVOIC_9100231.edi"));

            var inv = Assert.Single(invoices);
            Assert.Equal("9100231", inv.InvoiceNumber);
            Assert.Equal(new DateTime(2026, 10, 20), inv.InvoiceDate);
            Assert.Equal(new DateTime(2026, 11, 19), inv.DueDate);
            Assert.Equal("4500012001", inv.PurchaseOrderNumber);
            Assert.Equal("FI12345671", inv.SupplierVatNumber);
            Assert.Equal("Nordic Fasteners Oy", inv.SupplierName);
            Assert.Equal(218.00m, inv.NetAmount);
            Assert.Equal(55.59m, inv.VatAmount);
            Assert.Equal(273.59m, inv.GrossAmount);
            Assert.Equal(2, inv.Lines.Count);
            Assert.Equal("100-2050", inv.Lines[1].ItemNumber);
            Assert.Equal("NF-M8N", inv.Lines[1].SupplierItemNumber);
            Assert.Equal(1500m, inv.Lines[1].Quantity);
            Assert.Equal(2, inv.Lines[1].PurchaseOrderLine);
            Assert.Equal(25.5m, inv.Lines[0].VatRate);
            Assert.Equal("4500012001", inv.Lines[0].PurchaseOrderNumber);
        }

        [Fact]
        public void Edifact_release_character_and_default_separators()
        {
            const string edi = "UNB+UNOC:3+1+2+261020:0815+1'UNH+1+INVOIC:D:96A:UN'BGM+380+A?+B?'1+9'DTM+137:20261001:102'" +
                               "LIN+1++X1'QTY+47:2:PCE'PRI+AAA:5'TAX+7+VAT+++:::24'UNS+S'MOA+124:2.40'UNT+10+1'UNZ+1+1'";

            var inv = new EdifactInvoiceParser().Parse(edi).Single();

            Assert.Equal("A+B'1", inv.InvoiceNumber);
            Assert.Equal(10m, inv.Lines[0].NetAmount);
            Assert.Equal(10m, inv.NetAmount);
            Assert.Equal(12.40m, inv.GrossAmount);
        }

        [Fact]
        public void Edifact_segment_count_is_checked()
        {
            const string edi = "UNH+1+INVOIC:D:96A:UN'BGM+380+1+9'UNT+5+1'";
            var ex = Assert.Throws<InvoiceParseException>(() => new EdifactInvoiceParser().Parse(edi));
            Assert.Contains("declares 5", ex.Message);
        }

        [Fact]
        public void Edifact_interchange_with_two_messages()
        {
            const string edi = "UNH+1+INVOIC:D:96A:UN'BGM+380+INV1+9'DTM+137:20261001:102'UNT+4+1'" +
                               "UNH+2+INVOIC:D:96A:UN'BGM+381+CN1+9'DTM+137:20261002:102'UNT+4+2'";
            var list = new EdifactInvoiceParser().Parse(edi);
            Assert.Equal(new[] { "INV1", "CN1" }, list.Select(i => i.InvoiceNumber));
            Assert.Equal(DocumentType.CreditNote, list[1].Type);
        }

        [Fact]
        public void Edifact_other_message_type_is_rejected()
        {
            Assert.Throws<InvoiceParseException>(() => new EdifactInvoiceParser().Parse("UNH+1+ORDERS:D:96A:UN'UNT+2+1'"));
        }

        [Fact]
        public void Ubl_sample_is_parsed()
        {
            var inv = new UblInvoiceParser().Parse(Samples.Text("BP-2026-0815.xml")).Single();

            Assert.Equal("BP-2026-0815", inv.InvoiceNumber);
            Assert.Equal(new DateTime(2026, 11, 30), inv.DueDate);
            Assert.Equal("EE100234567", inv.SupplierVatNumber);
            Assert.Equal("Baltic Packaging AS", inv.SupplierName);
            Assert.Equal("4500012002", inv.PurchaseOrderNumber);
            Assert.Equal("RF471234567890", inv.PaymentReference);
            Assert.Equal(1246.00m, inv.NetAmount);
            Assert.Equal(317.73m, inv.VatAmount);
            Assert.Equal(1563.73m, inv.GrossAmount);
            Assert.Equal(13.40m, inv.Lines[1].UnitPrice);
            Assert.Equal("PK-1010", inv.Lines[1].ItemNumber);
            Assert.Equal(2, inv.Lines[1].PurchaseOrderLine);
            Assert.Equal("PCE", inv.Lines[0].Unit);
        }

        [Fact]
        public void Ubl_credit_note_amounts_are_negative()
        {
            const string xml = @"<CreditNote xmlns=""urn:oasis:names:specification:ubl:schema:xsd:CreditNote-2""
 xmlns:cac=""urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2""
 xmlns:cbc=""urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"">
 <cbc:ID>CN-9</cbc:ID><cbc:IssueDate>2026-10-01</cbc:IssueDate>
 <cac:TaxTotal><cbc:TaxAmount>2.40</cbc:TaxAmount></cac:TaxTotal>
 <cac:LegalMonetaryTotal><cbc:TaxExclusiveAmount>10.00</cbc:TaxExclusiveAmount><cbc:PayableAmount>12.40</cbc:PayableAmount></cac:LegalMonetaryTotal>
 <cac:CreditNoteLine><cbc:CreditedQuantity unitCode=""C62"">2</cbc:CreditedQuantity><cbc:LineExtensionAmount>10.00</cbc:LineExtensionAmount>
 <cac:Item><cbc:Name>X</cbc:Name><cac:ClassifiedTaxCategory><cbc:Percent>24</cbc:Percent></cac:ClassifiedTaxCategory></cac:Item>
 <cac:Price><cbc:PriceAmount>5</cbc:PriceAmount></cac:Price></cac:CreditNoteLine></CreditNote>";

            var inv = new UblInvoiceParser().Parse(xml).Single();

            Assert.Equal(DocumentType.CreditNote, inv.Type);
            Assert.Equal(-12.40m, inv.GrossAmount);
            Assert.Equal(-2m, inv.Lines[0].Quantity);
            Assert.Equal(-10m, inv.Lines[0].NetAmount);
        }

        [Fact]
        public void Ubl_with_dtd_is_rejected()
        {
            const string xml = "<?xml version=\"1.0\"?><!DOCTYPE x [<!ENTITY e SYSTEM \"file:///etc/passwd\">]><Invoice>&e;</Invoice>";
            Assert.Throws<InvoiceParseException>(() => new UblInvoiceParser().Parse(xml));
        }

        [Fact]
        public void Csv_sample_is_parsed_with_supplier_code_page()
        {
            var inv = new CsvInvoiceParser().Parse(Samples.Text("LOS_77810.csv")).Single();

            Assert.Equal("77810", inv.InvoiceNumber);
            Assert.Equal(new DateTime(2026, 10, 22), inv.InvoiceDate);
            Assert.Equal("FI 2345678-2", inv.SupplierVatNumber);
            Assert.Equal(791.00m, inv.NetAmount);
            Assert.Equal(201.71m, inv.VatAmount);
            Assert.Equal("Värikasetti HP 59A – musta", inv.Lines[1].Description);
            Assert.Equal(4.35m, inv.Lines[0].UnitPrice);
            Assert.Equal(25.5m, inv.Lines[0].VatRate);
            Assert.Equal(2, inv.Lines[1].PurchaseOrderLine);
        }

        [Fact]
        public void Csv_quoted_fields_and_errors()
        {
            var inv = new CsvInvoiceParser().Parse("H;1;01.10.2026;;FI1;\"A; B \"\"Oy\"\"\";PO;EUR;1;0;1;\nL;1;X;Y;1;PCE;1;1;0\n").Single();
            Assert.Equal("A; B \"Oy\"", inv.SupplierName);
            Assert.Null(inv.DueDate);

            var ex = Assert.Throws<InvoiceParseException>(() => new CsvInvoiceParser().Parse("L;1;X;Y;1;PCE;1;1;0\n"));
            Assert.Contains("before header", ex.Message);
        }

        [Theory]
        [InlineData("a.edi", "UNA:+.? 'UNB+", SourceFormat.Edifact)]
        [InlineData("a.dat", "UNB+UNOC:3", SourceFormat.Edifact)]
        [InlineData("a.dat", "﻿<?xml version", SourceFormat.Ubl)]
        [InlineData("a.dat", "H;123;", SourceFormat.Csv)]
        [InlineData("a.csv", "", SourceFormat.Csv)]
        [InlineData("a.pdf", "%PDF-1.4", SourceFormat.Unknown)]
        public void Format_detection(string name, string head, SourceFormat expected)
        {
            Assert.Equal(expected, InvoiceParsers.Detect(name, head));
        }

        [Fact]
        public void Edifact_unoc_is_decoded_as_latin1()
        {
            var bytes = System.Text.Encoding.Latin1.GetBytes("UNB+UNOC:3+1'NAD+SU+++Säätö Oy'");
            var text = InvoiceParsers.Decode(bytes, System.Text.Encoding.UTF8);
            Assert.Contains("Säätö", text);
        }
    }
}
