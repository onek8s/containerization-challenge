using System.Collections.Generic;
using System.IO;
using Fabrikam.Housing.Data.Queries;
using iTextSharp.text;
using iTextSharp.text.pdf;

namespace Fabrikam.Housing.Documents.Pdf
{
    /// <summary>Rent statement as a PDF table (the tenant portal download and the printed statement pack).</summary>
    public class StatementPdfWriter : IStatementPdfWriter
    {
        private static readonly BaseColor Brand = new BaseColor(0, 87, 107);

        public byte[] Write(StatementHeader header, IList<StatementLine> lines)
        {
            using (var output = new MemoryStream())
            {
                var document = new Document(PageSize.A4, 40, 40, 50, 50);
                var writer = PdfWriter.GetInstance(document, output);
                writer.CloseStream = false;
                writer.PageEvent = new Footer(header.TenancyReference);
                document.AddTitle("Rent statement " + header.TenancyReference);
                document.AddAuthor("Fabrikam Homes");
                document.Open();

                var title = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16, Brand);
                var normal = FontFactory.GetFont(FontFactory.HELVETICA, 9);
                var bold = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 9);
                var white = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 9, BaseColor.White);

                document.Add(new Paragraph("Fabrikam Homes", title));
                document.Add(new Paragraph("Rent statement", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12)));
                document.Add(new Paragraph(" "));
                var info = new PdfPTable(2) { WidthPercentage = 60, HorizontalAlignment = Element.ALIGN_LEFT };
                info.SetWidths(new[] { 1f, 2.2f });
                void Info(string label, string value)
                {
                    info.AddCell(new PdfPCell(new Phrase(label, bold)) { Border = 0 });
                    info.AddCell(new PdfPCell(new Phrase(value, normal)) { Border = 0 });
                }
                Info("Tenant", header.TenantName);
                Info("Address", header.Address);
                Info("Tenancy", header.TenancyReference);
                Info("Period", header.From.ToString("dd/MM/yyyy") + " to " + header.To.ToString("dd/MM/yyyy"));
                Info("Weekly charge", "£" + header.WeeklyCharge.ToString("N2"));
                document.Add(info);
                document.Add(new Paragraph(" "));

                var table = new PdfPTable(6) { WidthPercentage = 100, HeaderRows = 1 };
                table.SetWidths(new[] { 1.1f, 3.2f, 1.6f, 1f, 1f, 1.1f });
                foreach (var h in new[] { "Date", "Description", "Reference", "Debit", "Credit", "Balance" })
                    table.AddCell(new PdfPCell(new Phrase(h, white)) { BackgroundColor = Brand, Padding = 4 });

                var shade = false;
                foreach (var l in lines)
                {
                    var bg = shade ? new BaseColor(238, 244, 246) : BaseColor.White;
                    shade = !shade;
                    table.AddCell(Cell(l.PostedOn.ToString("dd/MM/yyyy"), normal, bg, Element.ALIGN_LEFT));
                    table.AddCell(Cell(l.Description, normal, bg, Element.ALIGN_LEFT));
                    table.AddCell(Cell(l.Reference, normal, bg, Element.ALIGN_LEFT));
                    table.AddCell(Cell(l.Debit == 0 ? "" : l.Debit.ToString("N2"), normal, bg, Element.ALIGN_RIGHT));
                    table.AddCell(Cell(l.Credit == 0 ? "" : l.Credit.ToString("N2"), normal, bg, Element.ALIGN_RIGHT));
                    table.AddCell(Cell(l.Balance.ToString("N2"), l.Balance > 0 ? bold : normal, bg, Element.ALIGN_RIGHT));
                }
                document.Add(table);

                var closing = lines.Count == 0 ? 0m : lines[lines.Count - 1].Balance;
                document.Add(new Paragraph(" "));
                document.Add(new Paragraph(closing > 0
                    ? $"Amount owing: £{closing:N2}. Please pay at tenants.fabrikamhomes.co.uk or call 0300 123 4567."
                    : $"Your account is in credit by £{-closing:N2}.", bold));
                document.Close();
                return output.ToArray();
            }
        }

        private static PdfPCell Cell(string text, Font font, BaseColor bg, int align) =>
            new PdfPCell(new Phrase(text ?? "", font)) { BackgroundColor = bg, HorizontalAlignment = align, Padding = 3, BorderColor = new BaseColor(200, 200, 200) };

        private class Footer : PdfPageEventHelper
        {
            private readonly string _reference;

            public Footer(string reference) { _reference = reference; }

            public override void OnEndPage(PdfWriter writer, Document document)
            {
                var font = FontFactory.GetFont(FontFactory.HELVETICA, 7, BaseColor.Gray);
                ColumnText.ShowTextAligned(writer.DirectContent, Element.ALIGN_CENTER,
                    new Phrase($"Fabrikam Homes - rent statement {_reference} - page {writer.PageNumber}", font),
                    (document.Left + document.Right) / 2, document.Bottom - 20, 0);
            }
        }
    }
}
