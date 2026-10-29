using System.Globalization;
using System.Text;

namespace FurnaceRecords.Core;

/// <summary>Writes single or multi page PDF documents with Helvetica text lines.</summary>
public sealed class SimplePdf
{
    private const double PageWidth = 595.28;
    private const double PageHeight = 841.89;
    private const double Margin = 50;

    private readonly List<List<(double Size, bool Bold, string Text)>> _pages = new() { new() };
    private double _y = PageHeight - Margin;

    public void Line(string text, double size = 10, bool bold = false)
    {
        var lead = size * 1.35;
        if (_y - lead < Margin)
        {
            _pages.Add(new());
            _y = PageHeight - Margin;
        }
        _y -= lead;
        _pages[^1].Add((size, bold, text));
    }

    public void Space(double points = 8) => _y -= points;

    public byte[] ToArray()
    {
        var objects = new List<string>();
        // 1 catalog, 2 pages, 3 regular font, 4 bold font, then page and content objects.
        var pageIds = Enumerable.Range(0, _pages.Count).Select(i => 5 + i * 2).ToList();
        objects.Add("<< /Type /Catalog /Pages 2 0 R >>");
        objects.Add($"<< /Type /Pages /Kids [{string.Join(" ", pageIds.Select(id => $"{id} 0 R"))}] /Count {_pages.Count} >>");
        objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>");

        foreach (var (lines, index) in _pages.Select((p, i) => (p, i)))
        {
            var content = new StringBuilder();
            var y = PageHeight - Margin;
            foreach (var (size, bold, text) in lines)
            {
                y -= size * 1.35;
                content.Append(CultureInfo.InvariantCulture, $"BT /{(bold ? "F2" : "F1")} {size:0.##} Tf {Margin:0.##} {y:0.##} Td ({Escape(text)}) Tj ET\n");
            }
            var stream = content.ToString();
            objects.Add(string.Create(CultureInfo.InvariantCulture,
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {PageWidth:0.##} {PageHeight:0.##}] /Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {pageIds[index] + 1} 0 R >>"));
            objects.Add($"<< /Length {Latin1.GetByteCount(stream)} >>\nstream\n{stream}endstream");
        }

        using var ms = new MemoryStream();
        void Write(string s) { var b = Latin1.GetBytes(s); ms.Write(b); }
        Write("%PDF-1.4\n");
        var offsets = new List<long>();
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(ms.Position);
            Write($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }
        var xref = ms.Position;
        Write($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var o in offsets) Write($"{o:0000000000} 00000 n \n");
        Write($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return ms.ToArray();
    }

    private static readonly Encoding Latin1 = Encoding.Latin1;

    private static string Escape(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            switch (c)
            {
                case '(' or ')' or '\\': sb.Append('\\').Append(c); break;
                case '°': sb.Append("\\260"); break;
                case '±': sb.Append("\\261"); break;
                case < ' ': break;
                case > 'ÿ': sb.Append('?'); break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }
}
