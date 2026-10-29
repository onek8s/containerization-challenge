using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace Wingtip.Invoices.Core.Parsing
{
    /// <summary>
    /// OASIS UBL 2.1 Invoice and CreditNote (Peppol BIS Billing 3.0 profile).
    /// </summary>
    public class UblInvoiceParser : IInvoiceParser
    {
        private static readonly XNamespace Cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
        private static readonly XNamespace Cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
        private const string InvoiceNs = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";
        private const string CreditNoteNs = "urn:oasis:names:specification:ubl:schema:xsd:CreditNote-2";

        public SourceFormat Format
        {
            get { return SourceFormat.Ubl; }
        }

        public IList<InvoiceDocument> Parse(string content)
        {
            XDocument xml;
            try
            {
                var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
                using (var reader = XmlReader.Create(new System.IO.StringReader(content), settings))
                {
                    xml = XDocument.Load(reader);
                }
            }
            catch (XmlException ex)
            {
                throw new InvoiceParseException("The file is not well-formed XML: " + ex.Message, ex);
            }

            var root = xml.Root;
            bool credit;
            if (root.Name.NamespaceName == InvoiceNs && root.Name.LocalName == "Invoice") credit = false;
            else if (root.Name.NamespaceName == CreditNoteNs && root.Name.LocalName == "CreditNote") credit = true;
            else throw new InvoiceParseException("Unsupported XML document " + root.Name + ".");

            var invoice = new InvoiceDocument
            {
                Type = credit ? DocumentType.CreditNote : DocumentType.Invoice,
                InvoiceNumber = Value(root, Cbc + "ID"),
                InvoiceDate = Date(Value(root, Cbc + "IssueDate")),
                Currency = Value(root, Cbc + "DocumentCurrencyCode") ?? "EUR",
                BuyerReference = Value(root, Cbc + "BuyerReference"),
                PurchaseOrderNumber = Value(root.Element(Cac + "OrderReference"), Cbc + "ID")
            };

            var due = Value(root, Cbc + "DueDate") ?? Value(root.Element(Cac + "PaymentMeans"), Cbc + "PaymentDueDate");
            if (due != null) invoice.DueDate = Date(due);
            invoice.PaymentReference = Value(root.Element(Cac + "PaymentMeans"), Cbc + "PaymentID");

            var supplier = root.Element(Cac + "AccountingSupplierParty");
            var party = supplier == null ? null : supplier.Element(Cac + "Party");
            if (party != null)
            {
                invoice.SupplierName = Value(party.Element(Cac + "PartyLegalEntity"), Cbc + "RegistrationName")
                    ?? Value(party.Element(Cac + "PartyName"), Cbc + "Name");
                invoice.SupplierVatNumber = party.Elements(Cac + "PartyTaxScheme")
                    .Select(t => Value(t, Cbc + "CompanyID"))
                    .FirstOrDefault(v => v != null);
            }

            var totals = root.Element(Cac + "LegalMonetaryTotal");
            invoice.NetAmount = Amount(Value(totals, Cbc + "TaxExclusiveAmount"));
            invoice.GrossAmount = Amount(Value(totals, Cbc + "PayableAmount") ?? Value(totals, Cbc + "TaxInclusiveAmount"));
            invoice.VatAmount = root.Elements(Cac + "TaxTotal").Select(t => Amount(Value(t, Cbc + "TaxAmount"))).FirstOrDefault();

            var lineName = credit ? "CreditNoteLine" : "InvoiceLine";
            var quantityName = credit ? "CreditedQuantity" : "InvoicedQuantity";
            var number = 0;
            foreach (var l in root.Elements(Cac + lineName))
            {
                number++;
                var qty = l.Element(Cbc + quantityName);
                var item = l.Element(Cac + "Item");
                var price = l.Element(Cac + "Price");
                var orderLine = l.Element(Cac + "OrderLineReference");
                var tax = item == null ? null : item.Element(Cac + "ClassifiedTaxCategory");

                var line = new InvoiceLine
                {
                    LineNumber = number,
                    Quantity = Amount(qty == null ? null : qty.Value),
                    Unit = UnitCode(qty == null ? null : (string)qty.Attribute("unitCode")),
                    NetAmount = Amount(Value(l, Cbc + "LineExtensionAmount")),
                    UnitPrice = Amount(Value(price, Cbc + "PriceAmount")),
                    VatRate = Amount(Value(tax, Cbc + "Percent")),
                    Description = Value(item, Cbc + "Name") ?? Value(item, Cbc + "Description"),
                    ItemNumber = Value(item == null ? null : item.Element(Cac + "BuyersItemIdentification"), Cbc + "ID"),
                    SupplierItemNumber = Value(item == null ? null : item.Element(Cac + "SellersItemIdentification"), Cbc + "ID"),
                    PurchaseOrderNumber = invoice.PurchaseOrderNumber
                };

                var baseQty = Value(price, Cbc + "BaseQuantity");
                if (baseQty != null && Amount(baseQty) != 0m) line.UnitPrice = line.UnitPrice / Amount(baseQty);

                var poLine = Value(orderLine, Cbc + "LineID");
                int poLineNumber;
                if (poLine != null && int.TryParse(poLine, NumberStyles.Integer, CultureInfo.InvariantCulture, out poLineNumber))
                    line.PurchaseOrderLine = poLineNumber;

                if (credit)
                {
                    line.Quantity = -Math.Abs(line.Quantity);
                    line.NetAmount = -Math.Abs(line.NetAmount);
                }
                invoice.Lines.Add(line);
            }

            if (credit)
            {
                invoice.NetAmount = -Math.Abs(invoice.NetAmount);
                invoice.VatAmount = -Math.Abs(invoice.VatAmount);
                invoice.GrossAmount = -Math.Abs(invoice.GrossAmount);
            }

            return new List<InvoiceDocument> { invoice };
        }

        private static string UnitCode(string code)
        {
            switch (code)
            {
                case null:
                case "C62":
                case "H87":
                    return "PCE";
                case "KGM":
                    return "KGM";
                case "MTR":
                    return "MTR";
                case "LTR":
                    return "LTR";
                default:
                    return code;
            }
        }

        private static string Value(XElement parent, XName name)
        {
            if (parent == null) return null;
            var e = parent.Element(name);
            if (e == null) return null;
            var v = e.Value.Trim();
            return v.Length == 0 ? null : v;
        }

        private static DateTime Date(string value)
        {
            DateTime d;
            if (value == null || !DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
                throw new InvoiceParseException("Invalid date '" + value + "'.");
            return d;
        }

        private static decimal Amount(string value)
        {
            if (value == null) return 0m;
            decimal d;
            if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out d))
                throw new InvoiceParseException("Invalid amount '" + value + "'.");
            return d;
        }
    }
}
