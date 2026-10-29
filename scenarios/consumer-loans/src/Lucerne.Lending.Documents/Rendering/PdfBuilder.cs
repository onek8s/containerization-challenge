using iTextSharp.text;
using iTextSharp.text.pdf;

namespace Lucerne.Lending.Documents.Rendering;

/// <summary>Small helper over iTextSharp (LGPL 4.1.6 line) for the bank's letter layout.</summary>
public sealed class PdfBuilder : IDisposable
{
    private static readonly BaseColor Brand = new(0, 70, 110);
    private readonly MemoryStream _stream = new();
    private readonly Document _document;
    private readonly PdfWriter _writer;

    public PdfBuilder(string title, string subject)
    {
        _document = new Document(PageSize.A4, 56, 56, 72, 64);
        _writer = PdfWriter.GetInstance(_document, _stream);
        _writer.PageEvent = new Footer();
        _document.AddTitle(title);
        _document.AddSubject(subject);
        _document.AddAuthor("Lucerne Savings Bank");
        _document.AddCreator("Lucerne Consumer Loans");
        _document.Open();
        var head = new PdfPTable(2) { WidthPercentage = 100 };
        head.SetWidths(new[] { 3f, 2f });
        head.AddCell(new PdfPCell(new Phrase("LUCERNE SAVINGS BANK", Font(14, true, Brand))) { Border = 0 });
        head.AddCell(new PdfPCell(new Phrase(DateTime.Today.ToString("d.M.yyyy"), Font(9))) { Border = 0, HorizontalAlignment = Element.ALIGN_RIGHT });
        _document.Add(head);
        _document.Add(new Paragraph(" "));
    }

    public static iTextSharp.text.Font Font(float size, bool bold = false, BaseColor? color = null) =>
        FontFactory.GetFont(BaseFont.HELVETICA, BaseFont.CP1252, BaseFont.NOT_EMBEDDED, size, bold ? iTextSharp.text.Font.BOLD : iTextSharp.text.Font.NORMAL, color ?? BaseColor.Black);

    public PdfBuilder Address(string name, string street, string postal, string city)
    {
        foreach (var line in new[] { name, street, $"{postal} {city}" })
            _document.Add(new Paragraph(line, Font(10)));
        _document.Add(new Paragraph(" "));
        return this;
    }

    public PdfBuilder Heading(string text)
    {
        _document.Add(new Paragraph(text, Font(13, true, Brand)) { SpacingBefore = 6, SpacingAfter = 6 });
        return this;
    }

    public PdfBuilder SubHeading(string text)
    {
        _document.Add(new Paragraph(text, Font(10.5f, true)) { SpacingBefore = 8, SpacingAfter = 3 });
        return this;
    }

    public PdfBuilder Text(string text, float size = 9.5f)
    {
        _document.Add(new Paragraph(text, Font(size)) { SpacingAfter = 4, Leading = size * 1.35f });
        return this;
    }

    public PdfBuilder KeyValues(IEnumerable<(string Key, string Value)> rows)
    {
        var table = new PdfPTable(2) { WidthPercentage = 100, SpacingAfter = 6 };
        table.SetWidths(new[] { 2f, 3f });
        foreach (var (k, v) in rows)
        {
            table.AddCell(new PdfPCell(new Phrase(k, Font(9, true))) { BorderColor = BaseColor.LightGray, Padding = 4 });
            table.AddCell(new PdfPCell(new Phrase(v, Font(9))) { BorderColor = BaseColor.LightGray, Padding = 4 });
        }
        _document.Add(table);
        return this;
    }

    public PdfBuilder Table(string[] headers, IEnumerable<string[]> rows, float[]? widths = null)
    {
        var table = new PdfPTable(headers.Length) { WidthPercentage = 100, HeaderRows = 1, SpacingAfter = 6 };
        if (widths != null) table.SetWidths(widths);
        foreach (var h in headers)
            table.AddCell(new PdfPCell(new Phrase(h, Font(8, true, BaseColor.White))) { BackgroundColor = Brand, Padding = 3 });
        foreach (var r in rows)
            foreach (var (c, i) in r.Select((c, i) => (c, i)))
                table.AddCell(new PdfPCell(new Phrase(c, Font(8))) { Padding = 2.5f, BorderColor = BaseColor.LightGray,
                    HorizontalAlignment = i == 0 ? Element.ALIGN_LEFT : Element.ALIGN_RIGHT });
        _document.Add(table);
        return this;
    }

    public PdfBuilder SignatureLines(IEnumerable<string> names)
    {
        _document.Add(new Paragraph(" "));
        foreach (var n in names)
        {
            _document.Add(new Paragraph("______________________________", Font(9)) { SpacingBefore = 18 });
            _document.Add(new Paragraph(n, Font(9)));
        }
        return this;
    }

    public PdfBuilder NewPage()
    {
        _document.NewPage();
        return this;
    }

    public byte[] Build()
    {
        _document.Close();
        return _stream.ToArray();
    }

    public void Dispose()
    {
        if (_document.IsOpen()) _document.Close();
        _stream.Dispose();
    }

    private sealed class Footer : PdfPageEventHelper
    {
        public override void OnEndPage(PdfWriter writer, Document document)
        {
            var cb = writer.DirectContent;
            var font = Font(7, color: BaseColor.Gray);
            ColumnText.ShowTextAligned(cb, Element.ALIGN_LEFT,
                new Phrase("Lucerne Savings Bank, Aleksanterinkatu 30, 00100 Helsinki, Business ID 0123456-7, www.lucernebank.example", font),
                document.LeftMargin, 36, 0);
            ColumnText.ShowTextAligned(cb, Element.ALIGN_RIGHT, new Phrase($"{writer.PageNumber}", font), document.PageSize.Width - document.RightMargin, 36, 0);
        }
    }
}
