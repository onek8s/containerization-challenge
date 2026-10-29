using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Fabrikam.Housing.Data.Queries;
using Fabrikam.Housing.Documents;
using Fabrikam.Housing.Documents.Letters;
using Fabrikam.Housing.Documents.Pdf;
using Fabrikam.Housing.Documents.Search;
using Fabrikam.Housing.Rent;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Fabrikam.Housing.Tests.Documents
{
    public class LetterAndPdfTests
    {
        private static LetterComposer Composer() => new LetterComposer(Options.Create(new DocumentsOptions()));

        private static LetterModel Model(string template, bool largePrint = false) => new LetterModel
        {
            TemplateCode = template,
            RecipientName = "Ms Amelia O'Brien",
            AddressLines = new List<string> { "14 Lark Rise", "", "Bristol", "BS13 7QA" },
            Date = new DateTime(2021, 4, 12),
            Reference = "T020044",
            LargePrint = largePrint,
            Fields = new Dictionary<string, string>
            {
                ["Salutation"] = "Ms O'Brien",
                ["PropertyAddress"] = "14 Lark Rise, Bristol BS13 7QA",
                ["Balance"] = "£412.50",
                ["WeeksInArrears"] = "3.4",
                ["RespondBy"] = "26 April 2021",
                ["OfficerName"] = "Dafydd Price <Income>",
                ["LastPaymentAmount"] = "£50.00",
                ["LastPaymentDate"] = "1 March 2021"
            }
        };

        [Fact]
        public void All_letter_templates_are_deployed()
        {
            Composer().TemplateCodes.Should().Contain(new[]
            {
                "ArrearsStage2", "ArrearsStage3", "GasAccess1", "GasAccess2", "GasAccess3", "RentStatementCover",
                "RentIncrease", "DirectDebitConfirmation", "RepairAppointment", "ContractorRemittance"
            }).And.NotContain("_Layout");
        }

        [Fact]
        public void Composer_merges_fields_into_the_layout_and_encodes_values()
        {
            var html = Composer().Compose(Model("ArrearsStage2"));
            html.Should().Contain("Fabrikam Homes").And.Contain("12 April 2021").And.Contain("T020044");
            html.Should().Contain("14 Lark Rise<br/>Bristol<br/>BS13 7QA");
            html.Should().Contain("<strong>&#163;412.50</strong>");
            html.Should().Contain("Ms O&#39;Brien").And.Contain("Dafydd Price &lt;Income&gt;");
            html.Should().Contain("class=\"standard\"");
            html.Should().NotContain("{{");
        }

        [Fact]
        public void Large_print_and_html_fields()
        {
            var model = Model("ContractorRemittance", largePrint: true);
            model.Fields["InvoiceTableHtml"] = "<table><tr><td>AVO-4001</td></tr></table>";
            var html = Composer().Compose(model);
            html.Should().Contain("class=\"large-print\"").And.Contain("<td>AVO-4001</td>");
        }

        [Fact]
        public void Unknown_template_throws()
        {
            Action act = () => Composer().Compose(Model("NoSuchLetter"));
            act.Should().Throw<System.IO.FileNotFoundException>();
        }

        [Fact]
        public void Basic_renderer_produces_a_pdf_with_the_letter_text()
        {
            var pdf = new BasicPdfRenderer().RenderHtml(Composer().Compose(Model("ArrearsStage3")), "Second reminder");
            Encoding.ASCII.GetString(pdf, 0, 5).Should().Be("%PDF-");
            TextExtractor.Pdf(pdf).Should().Contain("Notice of Seeking Possession");
        }

        [Fact]
        public void Wkhtmltopdf_engine_without_the_native_library_falls_back_to_the_basic_engine()
        {
            var renderer = new PdfRenderer(Options.Create(new DocumentsOptions { PdfEngine = "Wkhtmltopdf", NativeLibraryFolder = "no-such-folder" }), NullLogger<PdfRenderer>.Instance);
            var pdf = renderer.RenderHtml("<html><body><p>Gas safety check</p></body></html>", "Test");
            Encoding.ASCII.GetString(pdf, 0, 5).Should().Be("%PDF-");
        }

        [Fact]
        public void Statement_pdf_lists_the_transactions()
        {
            var lines = new List<StatementLine>
            {
                new StatementLine { PostedOn = new DateTime(2021, 4, 5), Description = "Balance brought forward", Balance = 20m },
                new StatementLine { PostedOn = new DateTime(2021, 4, 5), Type = TransactionType.RentCharge, Description = "Rent", Reference = "WK01/2021", Debit = 98.40m, Balance = 118.40m },
                new StatementLine { PostedOn = new DateTime(2021, 4, 8), Type = TransactionType.DirectDebit, Description = "Direct debit", Reference = "PM123", Credit = 98.40m, Balance = 20m }
            };
            var pdf = new StatementPdfWriter().Write(new StatementHeader
            {
                TenancyReference = "T020044", TenantName = "Ms Amelia O'Brien", Address = "14 Lark Rise, Bristol BS13 7QA",
                From = new DateTime(2021, 4, 1), To = new DateTime(2021, 4, 30), WeeklyCharge = 98.40m
            }, lines);
            Encoding.ASCII.GetString(pdf, 0, 5).Should().Be("%PDF-");
            var text = TextExtractor.Pdf(pdf);
            text.Should().Contain("Rent statement").And.Contain("T020044").And.Contain("WK01/2021").And.Contain("Amount owing");
        }

        [Fact]
        public void Text_extractor_handles_html_and_plain_text()
        {
            var x = new TextExtractor(NullLogger<TextExtractor>.Instance);
            x.Extract(Encoding.UTF8.GetBytes("<html><head><style>p{}</style></head><body><p>Damp &amp; mould</p></body></html>"), "text/html", "a.html").Should().Be("Damp & mould");
            x.Extract(Encoding.UTF8.GetBytes("hello"), "text/plain", "a.txt").Should().Be("hello");
            x.Extract(new byte[] { 1, 2 }, "application/pdf", "broken.pdf").Should().BeEmpty();
            x.Extract(new byte[] { 1, 2 }, "application/zip", "a.zip").Should().BeEmpty();
        }
    }
}
