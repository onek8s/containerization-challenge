using System.Globalization;
using System.Text;

namespace Litware.DocControl.Packaging;

/// <summary>Writes <c>manifest.csv</c>: semicolon separated, UTF-8 with BOM so Excel opens it directly.</summary>
public static class ManifestWriter
{
    public const string Header = "DocumentNumber;Revision;Title;File;Bytes;SHA256";

    public static void Write(TextWriter writer, IEnumerable<PackagedFile> files)
    {
        writer.WriteLine(Header);
        foreach (var f in files)
        {
            writer.Write(f.DocumentNumber); writer.Write(';');
            writer.Write(f.Revision); writer.Write(';');
            writer.Write(Quote(f.Title)); writer.Write(';');
            writer.Write(f.EntryName); writer.Write(';');
            writer.Write(f.Size.ToString(CultureInfo.InvariantCulture)); writer.Write(';');
            writer.WriteLine(f.Sha256);
        }
    }

    public static byte[] ToBytes(IEnumerable<PackagedFile> files)
    {
        using var ms = new MemoryStream();
        using (var w = new StreamWriter(ms, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)) { NewLine = "\r\n" })
            Write(w, files);
        return ms.ToArray();
    }

    private static string Quote(string value) =>
        value.IndexOfAny([';', '"', '\r', '\n']) < 0 ? value : "\"" + value.Replace("\"", "\"\"") + "\"";
}
