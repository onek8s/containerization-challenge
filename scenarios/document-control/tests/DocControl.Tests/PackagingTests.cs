using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Litware.DocControl.Documents;
using Litware.DocControl.Packaging;

namespace Litware.DocControl.Tests;

public class PackagingTests
{
    private static readonly TransmittalHeader Header = new("TR-P1042-00003", "P1042", "Bleach plant upgrade", "Issued for review",
        "Pohjola Pulp Oy", "Anna Laine", "Liisa Niemi", new DateTime(2024, 4, 3, 9, 0, 0, DateTimeKind.Utc), "Comments by 15.4.");

    [Fact]
    public void Writes_documents_manifest_and_cover_sheet()
    {
        var a = Encoding.ASCII.GetBytes("%PDF-1.4 drawing A");
        var b = new byte[100_000];
        new Random(4).NextBytes(b);

        var entries = new[]
        {
            new PackageEntry("P1042-LIT-ME-DRG-0012", "P02", "Bleach tower 2 GA", "P1042-LIT-ME-DRG-0012_P02.pdf", () => new MemoryStream(a)),
            new PackageEntry("P1042-LIT-EL-LST-0001", "P01", "Motor list; MCC-1", "P1042-LIT-EL-LST-0001_P01.xlsx", () => new MemoryStream(b)),
        };

        using var ms = new MemoryStream();
        var files = TransmittalPackageWriter.Write(ms, Header, entries);

        Assert.Equal(2, files.Count);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(b)), files[1].Sha256);

        ms.Position = 0;
        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
        var names = zip.Entries.Select(e => e.FullName).ToList();
        Assert.Contains("TR-P1042-00003/documents/P1042-LIT-ME-DRG-0012_P02.pdf", names);
        Assert.Contains("TR-P1042-00003/manifest.csv", names);
        Assert.Contains("TR-P1042-00003/TR-P1042-00003.pdf", names);

        using var reader = new StreamReader(zip.GetEntry("TR-P1042-00003/manifest.csv")!.Open());
        var lines = reader.ReadToEnd().Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(ManifestWriter.Header, lines[0]);
        Assert.Contains("\"Motor list; MCC-1\"", lines[2]);
    }

    [Fact]
    public void Cover_sheet_is_a_readable_pdf_with_title()
    {
        var files = Enumerable.Range(1, 120)
            .Select(i => new PackagedFile($"P1042-LIT-ME-DRG-{i:0000}", "P01", $"Drawing {i} (sheet {i})", $"documents/{i}.pdf", i * 1000, ""))
            .ToList();
        var pdf = CoverSheet.Render(Header, files);

        var text = Encoding.Latin1.GetString(pdf);
        Assert.StartsWith("%PDF-1.4", text);
        Assert.EndsWith("%%EOF\n", text);
        Assert.Contains("/Count 3", text); // 120 lines over three pages

        // xref offsets point at the objects
        var startxref = int.Parse(text[(text.LastIndexOf("startxref\n", StringComparison.Ordinal) + 10)..].Split('\n')[0]);
        Assert.StartsWith("xref", text[startxref..]);
        var firstOffset = int.Parse(text[(startxref + 5)..].Split('\n')[2][..10]);
        Assert.StartsWith("1 0 obj", text[firstOffset..]);

        using var stream = new MemoryStream(pdf);
        Assert.Equal("TR-P1042-00003", PdfTitleReader.ReadTitle(stream));
    }

    [Fact]
    public void Reads_escaped_and_hex_titles()
    {
        using var literal = new MemoryStream(Encoding.Latin1.GetBytes("%PDF-1.5\n1 0 obj << /Title (P1042-LIT-ME-DRG-0012 \\(GA\\)) >> endobj\n%%EOF"));
        Assert.Equal("P1042-LIT-ME-DRG-0012 (GA)", PdfTitleReader.ReadTitle(literal));

        var utf16 = Convert.ToHexString(new byte[] { 0xFE, 0xFF }.Concat(Encoding.BigEndianUnicode.GetBytes("P1042-LIT-PI-PID-0003")).ToArray());
        using var hex = new MemoryStream(Encoding.Latin1.GetBytes($"%PDF-1.7\n<< /Title <{utf16}> >>\n%%EOF"));
        Assert.Equal("P1042-LIT-PI-PID-0003", PdfTitleReader.ReadTitle(hex));

        using var notPdf = new MemoryStream(Encoding.ASCII.GetBytes("AC1032 dwg"));
        Assert.Null(PdfTitleReader.ReadTitle(notPdf));
    }
}
