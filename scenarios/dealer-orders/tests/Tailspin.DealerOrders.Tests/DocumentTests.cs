using System;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using OfficeOpenXml;
using Tailspin.DealerOrders.Configurator;
using Tailspin.DealerOrders.Documents.Excel;
using Tailspin.DealerOrders.Documents.Word;
using Tailspin.DealerOrders.Domain;
using Tailspin.DealerOrders.LabStubs;
using Tailspin.DealerOrders.Processing.Catalog;
using Xunit;

namespace Tailspin.DealerOrders.Tests
{
    public class DocumentTests
    {
        private static readonly string TemplateFolder = Path.Combine(AppContext.BaseDirectory, "templates");

        private static string BodyText(byte[] docx)
        {
            using (var stream = new MemoryStream(docx))
            using (var doc = WordprocessingDocument.Open(stream, false))
                return string.Join("\n", doc.MainDocumentPart.Document.Body.Descendants<Paragraph>().Select(p => string.Concat(p.Descendants<Text>().Select(t => t.Text))));
        }

        private static SalesDocumentModel Model(string language, bool withEndCustomer)
        {
            var dealer = LabData.Dealers().Single(d => d.AccountNumber == "D-10010");
            var model = new SalesDocumentModel
            {
                Number = "Q-2019-000042",
                Date = new DateTime(2019, 10, 15),
                Dealer = dealer,
                Language = language,
                Currency = "EUR",
                EndCustomer = withEndCustomer ? "Lapin Metsäpalvelu Oy" : null,
                DealerReference = "MKP-1907",
                ValidUntil = new DateTime(2019, 11, 14),
                EarliestDelivery = new DateTime(2020, 2, 13),
                DeliveryTerms = DeliveryTerms.Cpt,
                ExtraDiscountPercent = 2,
                ExtraDiscountAmount = 4500m,
                Freight = 1200m
            };
            model.Lines.Add(new DocumentLine { LineNumber = 1, ItemId = "TF14", Description = "Tailspin TF14 forwarder", OptionDescriptions = { "Crane TC110, 11.0 m reach", "Heavy duty bogie axle" }, Quantity = 1, ListPrice = 286600m, DiscountPercent = 19.9m, NetPrice = 229500m });
            model.Lines.Add(new DocumentLine { LineNumber = 2, ItemId = "P-10045", Description = "Hydraulic filter element", Quantity = 24, ListPrice = 55m, DiscountPercent = 25m, NetPrice = 41.25m });
            return model;
        }

        [Fact]
        public void All_templates_exist_with_the_fields_the_builder_fills()
        {
            foreach (var lang in TemplateFactory.Languages)
            {
                var quote = new WordTemplate(File.ReadAllBytes(Path.Combine(TemplateFolder, SalesDocumentBuilder.TemplateName("Quote", lang)))).Tags();
                Assert.Contains("line.net", quote);
                Assert.Contains("validity", quote);
                Assert.Contains("if:endcustomer", quote);
                var contract = new WordTemplate(File.ReadAllBytes(Path.Combine(TemplateFolder, SalesDocumentBuilder.TemplateName("DealerContract", lang)))).Tags();
                Assert.Contains("discount.percent", contract);
            }
        }

        [Fact]
        public void Quote_is_filled_with_repeated_lines_and_language_texts()
        {
            var builder = new SalesDocumentBuilder(name => File.ReadAllBytes(Path.Combine(TemplateFolder, name)));
            var text = BodyText(builder.Quote(Model("fi", withEndCustomer: true)));
            Assert.Contains("Tarjous", text);
            Assert.Contains("Q-2019-000042", text);
            Assert.Contains("Metsäkone Pohjola Oy", text);
            Assert.Contains("Lapin Metsäpalvelu Oy", text);
            Assert.Contains("  - Heavy duty bogie axle", text);
            Assert.Contains("229 500,00 EUR", text.Replace(' ', ' '));
            Assert.Contains("Tarjous on voimassa 14.11.2019 asti.", text);
            Assert.Contains("Arvioitu toimitusviikko 7/2020", text);
            Assert.DoesNotContain("[", text);
        }

        [Fact]
        public void Optional_blocks_are_removed_when_empty()
        {
            var builder = new SalesDocumentBuilder(name => File.ReadAllBytes(Path.Combine(TemplateFolder, name)));
            var text = BodyText(builder.OrderConfirmation(Model("en", withEndCustomer: false)));
            Assert.Contains("Order confirmation", text);
            Assert.Contains("We confirm your order MKP-1907 as follows.", text);
            Assert.DoesNotContain("End customer", text);
        }

        [Fact]
        public void Dealer_contract_lists_discount_groups()
        {
            var builder = new SalesDocumentBuilder(name => File.ReadAllBytes(Path.Combine(TemplateFolder, name)));
            var dealer = LabData.Dealers().Single(d => d.AccountNumber == "D-10045");
            var text = BodyText(builder.DealerContract(new DealerContractModel
            {
                Dealer = dealer, Year = 2020, Language = "de", AnnualTargetEur = 2400000m,
                Discounts = LabData.Discounts().Where(d => d.Tier == dealer.DiscountTier).ToList(), SignatoryName = "Kari Nieminen", SignatoryTitle = "Sales Director"
            }));
            Assert.Contains("Händlervertrag 2020", text);
            Assert.Contains("MACHINE", text);
            Assert.Contains("15 %", text);
            Assert.Contains("Kari Nieminen", text);
        }

        [Fact]
        public void Price_list_workbook_has_family_sheets_and_formulas()
        {
            var stores = InMemoryStores.WithLabData();
            var catalog = new FileCatalog(Lab.CatalogFolder);
            var dealer = stores.All().Single(d => d.AccountNumber == "D-10022");
            byte[] bytes;
            try
            {
                bytes = new PriceListWorkbook(new PriceCalculator(stores)).Build(dealer, catalog.All(), new DateTime(2019, 10, 15));
            }
            catch (TypeInitializationException ex) when (ex.ToString().Contains("gdiplus"))
            {
                // AutoFitColumns measures text with System.Drawing; skip where libgdiplus is not installed.
                return;
            }
            using (var package = new ExcelPackage(new MemoryStream(bytes)))
            {
                var names = package.Workbook.Worksheets.Select(w => w.Name).ToList();
                Assert.Equal(new[] { "Terms", "Harvester", "Forwarder", "CompactLoader", "Options" }, names);
                var forwarders = package.Workbook.Worksheets["Forwarder"];
                Assert.Equal("TF10", forwarders.Cells[2, 1].Value);
                Assert.StartsWith("ROUND(F2*(1-G2),0)", forwarders.Cells[2, 8].Formula);
                Assert.Equal("SEK", forwarders.Cells[2, 9].Value);
            }
        }
    }
}
