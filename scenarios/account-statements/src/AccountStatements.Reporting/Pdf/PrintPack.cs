using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace Woodgrove.Statements.Reporting.Pdf
{
    public class PrintPackItem
    {
        public string CustomerNo { get; set; }
        public string CustomerName { get; set; }
        public string CountryCode { get; set; }
        public byte[] Pdf { get; set; }
    }

    public class PrintPackResult
    {
        public byte[] Pdf { get; set; }
        public string Manifest { get; set; }
        public int Documents { get; set; }
        public int Sheets { get; set; }
        public int Pages { get; set; }
    }

    /// <summary>
    /// Combines statements to be printed and posted into one PDF for the print room: cover sheet, one bookmark per customer,
    /// blank backs so every customer starts on a new sheet (duplex), and an inserter mark with the sheet sequence on each page.
    /// Domestic letters come first, then foreign letters (different postage).
    /// </summary>
    public class PrintPackBuilder
    {
        public bool Duplex { get; set; } = true;
        public string FontFamily { get; set; } = "Segoe UI";
        public int MaxSheetsPerEnvelope { get; set; } = 8;

        public PrintPackResult Build(IEnumerable<PrintPackItem> items, string title, DateTime runDate, string homeCountry = "FI")
        {
            var ordered = items.OrderBy(i => string.Equals(i.CountryCode, homeCountry, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                               .ThenBy(i => i.CustomerNo, StringComparer.Ordinal).ToList();
            var output = new PdfDocument();
            output.Info.Title = title;
            output.Info.Creator = "Woodgrove Leasing Account Statements";
            output.Info.CreationDate = runDate;

            var cover = output.AddPage();
            cover.Size = PdfSharp.PageSize.A4;
            if (Duplex) output.AddPage().Size = PdfSharp.PageSize.A4;

            var manifest = new StringBuilder();
            manifest.AppendLine("Sequence;CustomerNo;CustomerName;Country;FirstPage;Pages;Sheets;Envelope");
            var sequence = 0;
            var sheets = 0;
            var marks = new List<Tuple<PdfPage, string>>();
            foreach (var item in ordered)
            {
                sequence++;
                using (var input = PdfReader.Open(new MemoryStream(item.Pdf), PdfDocumentOpenMode.Import))
                {
                    var firstPage = output.PageCount + 1;
                    var count = input.PageCount;
                    var itemSheets = Duplex ? (count + 1) / 2 : count;
                    var envelope = itemSheets > MaxSheetsPerEnvelope ? "C4" : "C5";
                    for (var p = 0; p < count; p++)
                    {
                        var page = output.AddPage(input.Pages[p]);
                        if (p == 0) output.Outlines.Add(item.CustomerNo + " " + item.CustomerName, page, true);
                        marks.Add(Tuple.Create(page, Mark(sequence, p + 1, count)));
                    }
                    if (Duplex && count % 2 == 1)
                    {
                        var blank = output.AddPage();
                        blank.Size = PdfSharp.PageSize.A4;
                    }
                    sheets += itemSheets;
                    manifest.AppendFormat(CultureInfo.InvariantCulture, "{0};{1};{2};{3};{4};{5};{6};{7}\r\n",
                        sequence, item.CustomerNo, Csv(item.CustomerName), item.CountryCode, firstPage, count, itemSheets, envelope);
                }
            }

            var font = new XFont(FontFamily, 6, XFontStyle.Regular, new XPdfFontOptions(PdfFontEncoding.Unicode));
            foreach (var mark in marks)
            {
                using (var gfx = XGraphics.FromPdfPage(mark.Item1, XGraphicsPdfPageOptions.Append))
                {
                    var state = gfx.Save();
                    gfx.RotateAtTransform(-90, new XPoint(12, mark.Item1.Height.Point - 60));
                    gfx.DrawString(mark.Item2, font, XBrushes.Black, new XPoint(12, mark.Item1.Height.Point - 60));
                    gfx.Restore(state);
                }
            }
            DrawCover(cover, title, runDate, ordered.Count, sheets, output.PageCount);

            using (var ms = new MemoryStream())
            {
                output.Save(ms, false);
                return new PrintPackResult { Pdf = ms.ToArray(), Manifest = manifest.ToString(), Documents = ordered.Count, Sheets = sheets, Pages = output.PageCount };
            }
        }

        /// <summary>Inserter mark: document sequence, page of document, last page flag.</summary>
        public static string Mark(int sequence, int page, int pages)
        {
            return string.Format(CultureInfo.InvariantCulture, "WGS {0:00000}-{1:00}/{2:00}{3}", sequence, page, pages, page == pages ? " E" : "");
        }

        void DrawCover(PdfPage page, string title, DateTime runDate, int documents, int sheets, int pages)
        {
            using (var gfx = XGraphics.FromPdfPage(page))
            {
                var big = new XFont(FontFamily, 22, XFontStyle.Bold, new XPdfFontOptions(PdfFontEncoding.Unicode));
                var normal = new XFont(FontFamily, 11, XFontStyle.Regular, new XPdfFontOptions(PdfFontEncoding.Unicode));
                var y = 120.0;
                gfx.DrawString("PRINT PACK", big, XBrushes.DarkBlue, new XPoint(60, y));
                y += 40;
                foreach (var line in new[]
                {
                    title,
                    "Run date: " + runDate.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture),
                    "Letters: " + documents.ToString(CultureInfo.InvariantCulture),
                    "Sheets (excluding this cover): " + sheets.ToString(CultureInfo.InvariantCulture),
                    "Pages in file: " + pages.ToString(CultureInfo.InvariantCulture),
                    Duplex ? "Print duplex, A4, 80 g, window envelope C5 (C4 when marked in the manifest)" : "Print simplex, A4, 80 g",
                    "Domestic letters first, then foreign letters."
                })
                {
                    gfx.DrawString(line, normal, XBrushes.Black, new XPoint(60, y));
                    y += 22;
                }
                gfx.DrawRectangle(XPens.DarkBlue, 40, 80, page.Width.Point - 80, y - 60);
            }
        }

        static string Csv(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.IndexOfAny(new[] { ';', '"', '\n' }) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
        }
    }
}
