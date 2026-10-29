using System;
using System.Linq;
using System.Text;

namespace VanArsdel.Integration.Messaging
{
    public class EncodingFixerSettings
    {
        /// <summary>Code page used when the data has no BOM and is not valid UTF-8 (1252 for most of our partners).</summary>
        public int FallbackCodePage { get; set; } = 1252;

        public bool RemoveControlCharacters { get; set; } = true;

        /// <summary>For EDI: drop CR/LF wrapped every 80 characters by old VAN mailboxes.</summary>
        public bool UnwrapFixedLines { get; set; }

        /// <summary>Replace characters outside the EDIFACT UNOC/X12 basic set with their closest ASCII form.</summary>
        public bool TransliterateForEdi { get; set; }
    }

    public class EncodingFixResult
    {
        public string DetectedEncoding { get; set; }
        public bool HadBom { get; set; }
        public int RemovedCharacters { get; set; }
        public byte[] Body { get; set; }
    }

    /// <summary>
    /// Decode stage logic of the VanArsdel Encoding Fixer pipeline component: detects UTF-8/UTF-16 BOMs and
    /// invalid UTF-8, re-reads such data in the fallback code page and emits UTF-8 without BOM. Strips NULs and other
    /// control characters (except tab/CR/LF) that partner systems leave in fixed width exports.
    /// </summary>
    public static class EncodingFixer
    {
        static EncodingFixer()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public static EncodingFixResult Fix(byte[] data, EncodingFixerSettings settings)
        {
            settings = settings ?? new EncodingFixerSettings();
            data = data ?? new byte[0];
            var result = new EncodingFixResult();
            string text;

            if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
            {
                result.HadBom = true;
                result.DetectedEncoding = "utf-8";
                text = Encoding.UTF8.GetString(data, 3, data.Length - 3);
            }
            else if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE)
            {
                result.HadBom = true;
                result.DetectedEncoding = "utf-16";
                text = Encoding.Unicode.GetString(data, 2, data.Length - 2);
            }
            else if (data.Length >= 2 && data[0] == 0xFE && data[1] == 0xFF)
            {
                result.HadBom = true;
                result.DetectedEncoding = "utf-16BE";
                text = Encoding.BigEndianUnicode.GetString(data, 2, data.Length - 2);
            }
            else if (IsValidUtf8(data))
            {
                result.DetectedEncoding = "utf-8";
                text = Encoding.UTF8.GetString(data);
            }
            else
            {
                var enc = Encoding.GetEncoding(settings.FallbackCodePage);
                result.DetectedEncoding = enc.WebName;
                text = enc.GetString(data);
            }

            var sb = new StringBuilder(text.Length);
            foreach (var ch in text)
            {
                if (settings.RemoveControlCharacters && char.IsControl(ch) && ch != '\t' && ch != '\r' && ch != '\n')
                {
                    result.RemovedCharacters++;
                    continue;
                }

                if (settings.UnwrapFixedLines && (ch == '\r' || ch == '\n'))
                {
                    continue;
                }

                if (settings.TransliterateForEdi)
                {
                    var t = Transliterate(ch);
                    if (t != null)
                    {
                        sb.Append(t);
                        continue;
                    }
                }

                sb.Append(ch);
            }

            result.Body = new UTF8Encoding(false).GetBytes(sb.ToString());
            return result;
        }

        public static bool IsValidUtf8(byte[] data)
        {
            var i = 0;
            while (i < data.Length)
            {
                var b = data[i];
                int extra;
                if (b < 0x80)
                {
                    i++;
                    continue;
                }

                if ((b & 0xE0) == 0xC0)
                {
                    extra = 1;
                }
                else if ((b & 0xF0) == 0xE0)
                {
                    extra = 2;
                }
                else if ((b & 0xF8) == 0xF0)
                {
                    extra = 3;
                }
                else
                {
                    return false;
                }

                for (var k = 1; k <= extra; k++)
                {
                    if (i + k >= data.Length || (data[i + k] & 0xC0) != 0x80)
                    {
                        return false;
                    }
                }

                i += extra + 1;
            }

            return true;
        }

        private static string Transliterate(char ch)
        {
            switch (ch)
            {
                case '‘':
                case '’':
                    return "'";
                case '“':
                case '”':
                    return "\"";
                case '–':
                case '—':
                    return "-";
                case ' ':
                    return " ";
                case '…':
                    return "...";
                default:
                    return null;
            }
        }

        public static string Describe(EncodingFixResult r)
        {
            return string.Join(", ", new[]
            {
                r.DetectedEncoding,
                r.HadBom ? "BOM" : null,
                r.RemovedCharacters > 0 ? r.RemovedCharacters + " control characters removed" : null
            }.Where(s => s != null));
        }
    }
}
