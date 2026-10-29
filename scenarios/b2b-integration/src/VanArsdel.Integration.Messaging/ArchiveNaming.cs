using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace VanArsdel.Integration.Messaging
{
    /// <summary>
    /// Archive path and duplicate key of a received message. Pattern macros: %PartnerId%, %MessageKind%, %yyyy%,
    /// %MM%, %dd%, %MessageID%, %SourceFileName%, %Ext%. The archive component writes below the archive root
    /// (\\files01\B2BArchive$ in production) with this relative path.
    /// </summary>
    public static class ArchiveNaming
    {
        public const string DefaultPattern = "%PartnerId%/%yyyy%/%MM%/%dd%/%MessageKind%_%MessageID%%Ext%";

        public static string Expand(string pattern, string partnerId, string messageKind, Guid messageId, string sourceFileName, DateTime receivedUtc, string ext)
        {
            var p = string.IsNullOrEmpty(pattern) ? DefaultPattern : pattern;
            return p.Replace("%PartnerId%", Safe(string.IsNullOrEmpty(partnerId) ? "UNKNOWN" : partnerId))
                .Replace("%MessageKind%", Safe(string.IsNullOrEmpty(messageKind) ? "Unknown" : messageKind))
                .Replace("%yyyy%", receivedUtc.ToString("yyyy", CultureInfo.InvariantCulture))
                .Replace("%MM%", receivedUtc.ToString("MM", CultureInfo.InvariantCulture))
                .Replace("%dd%", receivedUtc.ToString("dd", CultureInfo.InvariantCulture))
                .Replace("%MessageID%", messageId.ToString("D"))
                .Replace("%SourceFileName%", Safe(sourceFileName == null ? string.Empty : System.IO.Path.GetFileName(sourceFileName)))
                .Replace("%Ext%", string.IsNullOrEmpty(ext) ? ".dat" : ext);
        }

        public static string ExtensionFor(PayloadFormat format)
        {
            switch (format)
            {
                case PayloadFormat.Xml:
                    return ".xml";
                case PayloadFormat.X12:
                    return ".x12";
                case PayloadFormat.Edifact:
                    return ".edi";
                default:
                    return ".dat";
            }
        }

        /// <summary>SHA-256 of the body, hex. Combined with the interchange control number for duplicate detection.</summary>
        public static string Hash(byte[] body)
        {
            using (var sha = SHA256.Create())
            {
                var h = sha.ComputeHash(body ?? new byte[0]);
                var sb = new StringBuilder(h.Length * 2);
                foreach (var b in h)
                {
                    sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                }

                return sb.ToString();
            }
        }

        private static string Safe(string value)
        {
            var sb = new StringBuilder(value.Length);
            foreach (var ch in value)
            {
                sb.Append(char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' || ch == '.' ? ch : '_');
            }

            return sb.ToString();
        }
    }
}
