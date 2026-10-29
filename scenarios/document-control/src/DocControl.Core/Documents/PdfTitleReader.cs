using System.Text;
using System.Text.RegularExpressions;

namespace Litware.DocControl.Documents;

/// <summary>
/// Reads the /Title entry of a PDF document information dictionary. CAD plot drivers put the
/// document number from the title block there, which is used to check that the file name matches
/// the drawing. Only uncompressed info dictionaries are read; anything else returns null.
/// </summary>
public static partial class PdfTitleReader
{
    private const int TailBytes = 64 * 1024;
    private const int HeadBytes = 16 * 1024;

    [GeneratedRegex(@"/Title\s*\((?<t>(?:\\.|[^\\)])*)\)", RegexOptions.CultureInvariant)]
    private static partial Regex TitleLiteral();

    [GeneratedRegex(@"/Title\s*<(?<h>[0-9A-Fa-f\s]*)>", RegexOptions.CultureInvariant)]
    private static partial Regex TitleHex();

    public static string? ReadTitle(Stream stream)
    {
        if (!stream.CanSeek) throw new ArgumentException("Stream must be seekable.", nameof(stream));

        var head = ReadChunk(stream, 0, HeadBytes);
        if (!head.StartsWith("%PDF-", StringComparison.Ordinal)) return null;

        var tailStart = Math.Max(0, stream.Length - TailBytes);
        return FindTitle(ReadChunk(stream, tailStart, TailBytes)) ?? FindTitle(head);
    }

    private static string ReadChunk(Stream stream, long offset, int count)
    {
        stream.Position = offset;
        var buffer = new byte[Math.Min(count, stream.Length - offset)];
        var read = 0;
        while (read < buffer.Length)
        {
            var n = stream.Read(buffer, read, buffer.Length - read);
            if (n == 0) break;
            read += n;
        }
        return Encoding.Latin1.GetString(buffer, 0, read);
    }

    private static string? FindTitle(string text)
    {
        var literal = TitleLiteral().Matches(text).LastOrDefault();
        if (literal is not null) return Unescape(literal.Groups["t"].Value);

        var hex = TitleHex().Matches(text).LastOrDefault();
        if (hex is not null) return DecodeHex(hex.Groups["h"].Value);

        return null;
    }

    private static string Unescape(string s)
    {
        var sb = new StringBuilder(s.Length);
        for (var i = 0; i < s.Length; i++)
        {
            if (s[i] != '\\' || i == s.Length - 1) { sb.Append(s[i]); continue; }
            var c = s[++i];
            sb.Append(c switch { 'n' => '\n', 'r' => '\r', 't' => '\t', _ => c });
        }
        return DecodeTextString(Encoding.Latin1.GetBytes(sb.ToString()));
    }

    private static string DecodeHex(string hex)
    {
        hex = new string(hex.Where(Uri.IsHexDigit).ToArray());
        if (hex.Length % 2 == 1) hex += "0";
        return DecodeTextString(Convert.FromHexString(hex));
    }

    private static string DecodeTextString(byte[] bytes)
    {
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2).Trim();
        return Encoding.Latin1.GetString(bytes).Trim();
    }
}
