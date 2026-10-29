using System;
using System.Collections.Generic;
using System.IO;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Tailspin.DealerOrders.Documents.Word
{
    /// <summary>
    /// Creates the starting versions of the Word templates (content controls with the tags the builders fill). Product
    /// management restyles them in Word; the tags must stay. Run "LabTools make-templates" to recreate them.
    /// </summary>
    public static class TemplateFactory
    {
        private static readonly Dictionary<string, Dictionary<string, string>> Labels = new Dictionary<string, Dictionary<string, string>>
        {
            ["en"] = new Dictionary<string, string>
            {
                ["number"] = "Number", ["date"] = "Date", ["dealer"] = "Dealer", ["account"] = "Account", ["reference"] = "Your reference", ["delivery"] = "Delivery address",
                ["endcustomer"] = "End customer", ["salesperson"] = "Sales contact", ["item"] = "Item", ["description"] = "Description", ["qty"] = "Qty", ["list"] = "List price",
                ["discount"] = "Discount", ["net"] = "Net amount", ["shipdate"] = "Ship date", ["subtotal"] = "Subtotal", ["extra"] = "Extra discount", ["freight"] = "Freight",
                ["total"] = "Total", ["terms"] = "Terms", ["deliveryterms"] = "Delivery terms", ["paymentterms"] = "Payment terms", ["axref"] = "Tailspin order",
                ["territory"] = "Territory", ["tier"] = "Dealer tier", ["period"] = "Agreement period", ["target"] = "Annual purchase target", ["creditlimit"] = "Credit limit",
                ["group"] = "Discount group", ["percent"] = "Discount", ["signature"] = "For Tailspin Machinery Oy", ["dealersignature"] = "For the dealer", ["notes"] = "Notes",
                ["footer"] = "Tailspin Machinery Oy · Teollisuuskatu 8 · 81100 Kontiolahti · Finland · VAT FI24816357"
            },
            ["fi"] = new Dictionary<string, string>
            {
                ["number"] = "Numero", ["date"] = "Päivämäärä", ["dealer"] = "Jälleenmyyjä", ["account"] = "Asiakasnumero", ["reference"] = "Viitteenne", ["delivery"] = "Toimitusosoite",
                ["endcustomer"] = "Loppuasiakas", ["salesperson"] = "Myyjä", ["item"] = "Nimike", ["description"] = "Kuvaus", ["qty"] = "Määrä", ["list"] = "Listahinta",
                ["discount"] = "Alennus", ["net"] = "Nettosumma", ["shipdate"] = "Toimituspäivä", ["subtotal"] = "Välisumma", ["extra"] = "Lisäalennus", ["freight"] = "Rahti",
                ["total"] = "Yhteensä", ["terms"] = "Ehdot", ["deliveryterms"] = "Toimitusehto", ["paymentterms"] = "Maksuehto", ["axref"] = "Tailspin-tilaus",
                ["territory"] = "Alue", ["tier"] = "Jälleenmyyjäluokka", ["period"] = "Sopimuskausi", ["target"] = "Vuosittainen ostotavoite", ["creditlimit"] = "Luottoraja",
                ["group"] = "Alennusryhmä", ["percent"] = "Alennus", ["signature"] = "Tailspin Machinery Oy:n puolesta", ["dealersignature"] = "Jälleenmyyjän puolesta", ["notes"] = "Huomautukset",
                ["footer"] = "Tailspin Machinery Oy · Teollisuuskatu 8 · 81100 Kontiolahti · Y-tunnus 2481635-7"
            },
            ["sv"] = new Dictionary<string, string>
            {
                ["number"] = "Nummer", ["date"] = "Datum", ["dealer"] = "Återförsäljare", ["account"] = "Kundnummer", ["reference"] = "Er referens", ["delivery"] = "Leveransadress",
                ["endcustomer"] = "Slutkund", ["salesperson"] = "Säljare", ["item"] = "Artikel", ["description"] = "Beskrivning", ["qty"] = "Antal", ["list"] = "Listpris",
                ["discount"] = "Rabatt", ["net"] = "Nettobelopp", ["shipdate"] = "Leveransdatum", ["subtotal"] = "Delsumma", ["extra"] = "Extra rabatt", ["freight"] = "Frakt",
                ["total"] = "Totalt", ["terms"] = "Villkor", ["deliveryterms"] = "Leveransvillkor", ["paymentterms"] = "Betalningsvillkor", ["axref"] = "Tailspin-order",
                ["territory"] = "Område", ["tier"] = "Återförsäljarklass", ["period"] = "Avtalsperiod", ["target"] = "Årligt inköpsmål", ["creditlimit"] = "Kreditgräns",
                ["group"] = "Rabattgrupp", ["percent"] = "Rabatt", ["signature"] = "För Tailspin Machinery Oy", ["dealersignature"] = "För återförsäljaren", ["notes"] = "Anmärkningar",
                ["footer"] = "Tailspin Machinery Oy · Teollisuuskatu 8 · 81100 Kontiolahti · Finland · VAT FI24816357"
            },
            ["de"] = new Dictionary<string, string>
            {
                ["number"] = "Nummer", ["date"] = "Datum", ["dealer"] = "Händler", ["account"] = "Kundennummer", ["reference"] = "Ihre Referenz", ["delivery"] = "Lieferadresse",
                ["endcustomer"] = "Endkunde", ["salesperson"] = "Ansprechpartner", ["item"] = "Artikel", ["description"] = "Beschreibung", ["qty"] = "Menge", ["list"] = "Listenpreis",
                ["discount"] = "Rabatt", ["net"] = "Nettobetrag", ["shipdate"] = "Liefertermin", ["subtotal"] = "Zwischensumme", ["extra"] = "Zusatzrabatt", ["freight"] = "Fracht",
                ["total"] = "Gesamt", ["terms"] = "Bedingungen", ["deliveryterms"] = "Lieferbedingung", ["paymentterms"] = "Zahlungsbedingung", ["axref"] = "Tailspin-Auftrag",
                ["territory"] = "Gebiet", ["tier"] = "Händlerstufe", ["period"] = "Vertragszeitraum", ["target"] = "Jährliches Einkaufsziel", ["creditlimit"] = "Kreditlimit",
                ["group"] = "Rabattgruppe", ["percent"] = "Rabatt", ["signature"] = "Für Tailspin Machinery Oy", ["dealersignature"] = "Für den Händler", ["notes"] = "Bemerkungen",
                ["footer"] = "Tailspin Machinery Oy · Teollisuuskatu 8 · 81100 Kontiolahti · Finnland · USt-IdNr. FI24816357"
            }
        };

        public static IEnumerable<string> Languages => Labels.Keys;

        public static void WriteAll(string folder)
        {
            Directory.CreateDirectory(folder);
            foreach (var lang in Labels.Keys)
            {
                File.WriteAllBytes(Path.Combine(folder, SalesDocumentBuilder.TemplateName("Quote", lang)), SalesDocument(lang, quote: true));
                File.WriteAllBytes(Path.Combine(folder, SalesDocumentBuilder.TemplateName("OrderConfirmation", lang)), SalesDocument(lang, quote: false));
                File.WriteAllBytes(Path.Combine(folder, SalesDocumentBuilder.TemplateName("DealerContract", lang)), Contract(lang));
            }
        }

        public static byte[] SalesDocument(string lang, bool quote)
        {
            var l = Labels[lang];
            return Create(body =>
            {
                body.Append(Heading(Field("title", "Title")));
                body.Append(KeyValueTable(l, new[]
                {
                    Tuple.Create("number", "number"), Tuple.Create("date", "date"), Tuple.Create("dealer", "dealer.name"), Tuple.Create("account", "dealer.account"),
                    Tuple.Create("reference", "dealer.reference"), Tuple.Create(quote ? "salesperson" : "axref", quote ? "salesperson" : "axreference"),
                    Tuple.Create("delivery", "delivery.address")
                }));
                body.Append(Conditional("endcustomer", Para(Bold(l["endcustomer"] + ": "), Field("endcustomer", "End customer"))));
                if (!quote) body.Append(Para(Field("confirmed", "Confirmation text")));
                body.Append(LinesTable(l, includeShipDate: !quote));
                body.Append(TotalsTable(l));
                body.Append(Para(Bold(l["terms"])));
                body.Append(Para(Text(l["deliveryterms"] + ": "), Field("deliveryterms", "Delivery terms")));
                body.Append(Para(Text(l["paymentterms"] + ": "), Field("paymentterms", "Payment terms")));
                if (quote)
                {
                    body.Append(Para(Field("validity", "Validity")));
                    body.Append(Para(Field("leadtime", "Lead time")));
                }
                body.Append(Para(Field("warranty", "Warranty")));
                body.Append(Conditional("notes", Para(Bold(l["notes"] + ": "), Field("notes", "Notes"))));
            }, l["footer"]);
        }

        public static byte[] Contract(string lang)
        {
            var l = Labels[lang];
            return Create(body =>
            {
                body.Append(Heading(Field("title", "Title")));
                body.Append(KeyValueTable(l, new[]
                {
                    Tuple.Create("dealer", "dealer.name"), Tuple.Create("account", "dealer.account"), Tuple.Create("delivery", "dealer.address"),
                    Tuple.Create("territory", "territory"), Tuple.Create("tier", "tier"), Tuple.Create("period", "period"), Tuple.Create("target", "target"),
                    Tuple.Create("paymentterms", "paymentterms"), Tuple.Create("creditlimit", "creditlimit"), Tuple.Create("deliveryterms", "deliveryterms")
                }));
                var table = NewTable(2);
                table.Append(HeaderRow(l["group"], l["percent"]));
                table.Append(new TableRow(Cell(CellField("discount.group")), Cell(CellField("discount.percent"))));
                body.Append(table);
                body.Append(Para(Field("warranty", "Warranty")));
                body.Append(Para(Text(" ")));
                var signatures = NewTable(2);
                signatures.Append(new TableRow(Cell(Para(Text(l["signature"]))), Cell(Para(Text(l["dealersignature"])))));
                signatures.Append(new TableRow(Cell(Para(Text(" ")), Para(Text("______________________________")), Para(Field("signatory.name", "Name")), Para(Field("signatory.title", "Title"))),
                    Cell(Para(Text(" ")), Para(Text("______________________________")))));
                body.Append(signatures);
            }, l["footer"]);
        }

        private static byte[] Create(Action<Body> build, string footerText)
        {
            using (var stream = new MemoryStream())
            {
                using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
                {
                    var main = doc.AddMainDocumentPart();
                    var styles = main.AddNewPart<StyleDefinitionsPart>();
                    styles.Styles = new Styles(new DocDefaults(
                        new RunPropertiesDefault(new RunPropertiesBaseStyle(new RunFonts { Ascii = "Calibri", HighAnsi = "Calibri" }, new FontSize { Val = "20" })),
                        new ParagraphPropertiesDefault(new SpacingBetweenLines { After = "80" })));
                    var settings = main.AddNewPart<DocumentSettingsPart>();
                    settings.Settings = new Settings();
                    var footer = main.AddNewPart<FooterPart>();
                    footer.Footer = new Footer(new Paragraph(new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                        new Run(new RunProperties(new FontSize { Val = "16" }), new Text(footerText))));
                    var body = new Body();
                    build(body);
                    body.Append(new SectionProperties(
                        new FooterReference { Type = HeaderFooterValues.Default, Id = main.GetIdOfPart(footer) },
                        new PageSize { Width = 11906, Height = 16838 },
                        new PageMargin { Top = 1134, Bottom = 1134, Left = 1134, Right = 1134, Header = 567, Footer = 567 }));
                    main.Document = new Document(body);
                    doc.PackageProperties.Creator = "Tailspin Machinery";
                    doc.PackageProperties.Title = "Dealer Orders template";
                }
                return stream.ToArray();
            }
        }

        private static Table KeyValueTable(Dictionary<string, string> l, IEnumerable<Tuple<string, string>> rows)
        {
            var table = NewTable(2, borders: false);
            foreach (var r in rows)
                table.Append(new TableRow(Cell(Para(Bold(l[r.Item1])), 2800), Cell(CellField(r.Item2), 6800)));
            return table;
        }

        private static Table LinesTable(Dictionary<string, string> l, bool includeShipDate)
        {
            var columns = includeShipDate ? 7 : 6;
            var table = NewTable(columns);
            var headers = new List<string> { "#", l["item"], l["description"], l["qty"], l["list"], l["discount"], l["net"] };
            if (includeShipDate) headers[4] = l["shipdate"];
            table.Append(HeaderRow(headers.GetRange(0, columns).ToArray()));
            var row = new TableRow(Cell(CellField("line.no")), Cell(CellField("line.item")), Cell(CellField("line.description")), Cell(CellField("line.qty")));
            if (includeShipDate) row.Append(Cell(CellField("line.shipdate")));
            else row.Append(Cell(CellField("line.list")));
            row.Append(Cell(CellField("line.discount")));
            row.Append(Cell(CellField("line.net")));
            table.Append(row);
            return table;
        }

        private static Table TotalsTable(Dictionary<string, string> l)
        {
            var table = NewTable(2, borders: false);
            table.Append(new TableRow(Cell(Para(Text(l["subtotal"])), 7000), Cell(CellField("subtotal"), 2600)));
            table.Append(new TableRow(Cell(Para(Text(l["extra"])), 7000), Cell(CellField("extradiscount"), 2600)));
            table.Append(new TableRow(Cell(Para(Text(l["freight"])), 7000), Cell(CellField("freight"), 2600)));
            table.Append(new TableRow(Cell(Para(Bold(l["total"])), 7000), Cell(CellField("total"), 2600)));
            return table;
        }

        private static Table NewTable(int columns, bool borders = true)
        {
            var props = new TableProperties(new TableWidth { Type = TableWidthUnitValues.Pct, Width = "5000" });
            if (borders)
                props.Append(new TableBorders(
                    new TopBorder { Val = BorderValues.Single, Size = 4 }, new BottomBorder { Val = BorderValues.Single, Size = 4 },
                    new InsideHorizontalBorder { Val = BorderValues.Single, Size = 2 }));
            var table = new Table(props);
            var grid = new TableGrid();
            for (var i = 0; i < columns; i++) grid.Append(new GridColumn());
            table.Append(grid);
            return table;
        }

        private static TableRow HeaderRow(params string[] titles)
        {
            var row = new TableRow(new TableRowProperties(new TableHeader()));
            foreach (var t in titles) row.Append(Cell(Para(Bold(t))));
            return row;
        }

        private static TableCell Cell(params OpenXmlElement[] content) => new TableCell(content);

        private static TableCell Cell(OpenXmlElement content, int width) =>
            new TableCell(new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Dxa, Width = width.ToString() }), content);

        private static Paragraph Heading(OpenXmlElement content) =>
            new Paragraph(new ParagraphProperties(new SpacingBetweenLines { After = "240" }), Wrap(content, new RunProperties(new Bold(), new FontSize { Val = "32" })));

        private static OpenXmlElement Wrap(OpenXmlElement element, RunProperties properties)
        {
            foreach (var run in element.Descendants<Run>()) run.PrependChild((RunProperties)properties.CloneNode(true));
            return element;
        }

        private static Paragraph Para(params OpenXmlElement[] content) => new Paragraph(content);

        private static Run Text(string text) => new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve });

        private static Run Bold(string text) => new Run(new RunProperties(new Bold()), new Text(text) { Space = SpaceProcessingModeValues.Preserve });

        /// <summary>Inline (run level) plain text content control.</summary>
        private static SdtRun Field(string tag, string placeholder) =>
            new SdtRun(new SdtProperties(new Tag { Val = tag }, new SdtAlias { Val = tag }, new ShowingPlaceholder(), new SdtContentText()),
                new SdtContentRun(new Run(new Text("[" + placeholder + "]"))));

        /// <summary>Cell level content control around a paragraph.</summary>
        private static SdtBlock CellField(string tag) =>
            new SdtBlock(new SdtProperties(new Tag { Val = tag }, new SdtAlias { Val = tag }, new ShowingPlaceholder(), new SdtContentText()),
                new SdtContentBlock(new Paragraph(new Run(new Text("[" + tag + "]")))));

        /// <summary>Block that is removed when its flag is false.</summary>
        private static SdtBlock Conditional(string flag, Paragraph content) =>
            new SdtBlock(new SdtProperties(new Tag { Val = "if:" + flag }, new SdtAlias { Val = "if:" + flag }), new SdtContentBlock(content));
    }
}
