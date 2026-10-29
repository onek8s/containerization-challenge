using System.Globalization;
using System.Text;

namespace Litware.DocControl.Packaging;

/// <summary>
/// Very small PDF writer for the transmittal cover sheet: A4 pages, Helvetica / Helvetica-Bold,
/// text and lines only. Keeps the cover sheet independent of any reporting component.
/// </summary>
public sealed class SimplePdfWriter
{
    public const double PageWidth = 595.28;
    public const double PageHeight = 841.89;

    private readonly List<StringBuilder> _pages = new();
    private StringBuilder? _current;

    public string? Title { get; set; }
    public string? Author { get; set; }

    public int PageCount => _pages.Count;

    public void NewPage()
    {
        _current = new StringBuilder();
        _pages.Add(_current);
    }

    public void Text(double x, double y, string text, double size = 10, bool bold = false)
    {
        var page = _current ?? throw new InvalidOperationException("Call NewPage first.");
        page.Append("BT /").Append(bold ? "F2" : "F1").Append(' ').Append(Num(size)).Append(" Tf ")
            .Append(Num(x)).Append(' ').Append(Num(PageHeight - y)).Append(" Td (")
            .Append(Escape(text)).Append(") Tj ET\n");
    }

    public void Line(double x1, double y1, double x2, double y2, double width = 0.5)
    {
        var page = _current ?? throw new InvalidOperationException("Call NewPage first.");
        page.Append(Num(width)).Append(" w ")
            .Append(Num(x1)).Append(' ').Append(Num(PageHeight - y1)).Append(" m ")
            .Append(Num(x2)).Append(' ').Append(Num(PageHeight - y2)).Append(" l S\n");
    }

    public byte[] ToArray()
    {
        if (_pages.Count == 0) NewPage();

        var latin1 = Encoding.Latin1;
        using var ms = new MemoryStream();
        var offsets = new List<long>();

        void Write(string s) { var b = latin1.GetBytes(s); ms.Write(b, 0, b.Length); }
        void Obj(string body) { offsets.Add(ms.Position); Write($"{offsets.Count} 0 obj\n{body}\nendobj\n"); }

        Write("%PDF-1.4\n%âãÏÓ\n");

        // 1 catalog, 2 pages, 3 F1, 4 F2, 5 info, then page/content pairs
        var firstPage = 6;
        var kids = string.Join(' ', Enumerable.Range(0, _pages.Count).Select(i => $"{firstPage + i * 2} 0 R"));

        Obj("<< /Type /Catalog /Pages 2 0 R >>");
        Obj($"<< /Type /Pages /Kids [{kids}] /Count {_pages.Count} >>");
        Obj("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        Obj("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>");
        Obj($"<< /Title ({Escape(Title ?? "")}) /Author ({Escape(Author ?? "")}) /Producer (Litware Document Control) /CreationDate (D:{DateTime.UtcNow:yyyyMMddHHmmss}Z) >>");

        for (var i = 0; i < _pages.Count; i++)
        {
            var content = _pages[i].ToString();
            var contentObj = firstPage + i * 2 + 1;
            Obj($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {Num(PageWidth)} {Num(PageHeight)}] /Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {contentObj} 0 R >>");
            Obj($"<< /Length {latin1.GetByteCount(content)} >>\nstream\n{content}endstream");
        }

        var xref = ms.Position;
        var sb = new StringBuilder();
        sb.Append("xref\n0 ").Append(offsets.Count + 1).Append('\n');
        sb.Append("0000000000 65535 f \n");
        foreach (var o in offsets) sb.Append(o.ToString("0000000000", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        sb.Append("trailer\n<< /Size ").Append(offsets.Count + 1).Append(" /Root 1 0 R /Info 5 0 R >>\nstartxref\n").Append(xref).Append("\n%%EOF\n");
        Write(sb.ToString());

        return ms.ToArray();
    }

    private static string Num(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    private static string Escape(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            switch (c)
            {
                case '\\': sb.Append(@"\\"); break;
                case '(': sb.Append(@"\("); break;
                case ')': sb.Append(@"\)"); break;
                case '\r': case '\n': sb.Append(' '); break;
                default: sb.Append(c <= 'ÿ' ? c : '?'); break;
            }
        }
        return sb.ToString();
    }
}
