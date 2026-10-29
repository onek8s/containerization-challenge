using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Tailspin.DealerOrders.Edi.As2
{
    /// <summary>
    /// Minimal MIME entity for AS2: headers plus raw body bytes, with multipart splitting. Bodies are kept as bytes
    /// because signatures and MICs are computed over the exact canonical (CRLF) form.
    /// </summary>
    public sealed class MimeEntity
    {
        private static readonly Encoding Latin1 = Encoding.GetEncoding("ISO-8859-1");

        public MimeEntity()
        {
            Headers = new List<KeyValuePair<string, string>>();
        }

        public List<KeyValuePair<string, string>> Headers { get; }
        public byte[] Body { get; set; }

        public string ContentType
        {
            get => Header("Content-Type");
            set => SetHeader("Content-Type", value);
        }

        public string MediaType => (ContentType ?? "text/plain").Split(';')[0].Trim().ToLowerInvariant();

        public string Header(string name) =>
            Headers.Where(h => string.Equals(h.Key, name, StringComparison.OrdinalIgnoreCase)).Select(h => h.Value).FirstOrDefault();

        public void SetHeader(string name, string value)
        {
            Headers.RemoveAll(h => string.Equals(h.Key, name, StringComparison.OrdinalIgnoreCase));
            Headers.Add(new KeyValuePair<string, string>(name, value));
        }

        public string Parameter(string name) => Parameter(ContentType, name);

        public static string Parameter(string headerValue, string name)
        {
            if (headerValue == null) return null;
            foreach (var part in headerValue.Split(';').Skip(1))
            {
                var idx = part.IndexOf('=');
                if (idx < 0) continue;
                if (!string.Equals(part.Substring(0, idx).Trim(), name, StringComparison.OrdinalIgnoreCase)) continue;
                return part.Substring(idx + 1).Trim().Trim('"');
            }
            return null;
        }

        /// <summary>Headers and body in canonical form (CRLF line ends), as signed and as sent.</summary>
        public byte[] ToBytes()
        {
            using (var ms = new MemoryStream())
            {
                var head = new StringBuilder();
                foreach (var h in Headers) head.Append(h.Key).Append(": ").Append(h.Value).Append("\r\n");
                head.Append("\r\n");
                var headBytes = Latin1.GetBytes(head.ToString());
                ms.Write(headBytes, 0, headBytes.Length);
                if (Body != null) ms.Write(Body, 0, Body.Length);
                return ms.ToArray();
            }
        }

        public static MimeEntity Parse(byte[] data)
        {
            var entity = new MimeEntity();
            var text = Latin1.GetString(data);
            var split = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            var sepLength = 4;
            if (split < 0)
            {
                split = text.IndexOf("\n\n", StringComparison.Ordinal);
                sepLength = 2;
            }
            if (split < 0) throw new FormatException("MIME entity has no header/body separator.");

            string currentName = null;
            var currentValue = new StringBuilder();
            foreach (var line in text.Substring(0, split).Replace("\r\n", "\n").Split('\n'))
            {
                if (line.Length > 0 && (line[0] == ' ' || line[0] == '\t') && currentName != null)
                {
                    currentValue.Append(' ').Append(line.Trim());
                    continue;
                }
                if (currentName != null) entity.Headers.Add(new KeyValuePair<string, string>(currentName, currentValue.ToString()));
                var idx = line.IndexOf(':');
                if (idx <= 0) { currentName = null; continue; }
                currentName = line.Substring(0, idx).Trim();
                currentValue.Clear().Append(line.Substring(idx + 1).Trim());
            }
            if (currentName != null) entity.Headers.Add(new KeyValuePair<string, string>(currentName, currentValue.ToString()));

            var bodyStart = Latin1.GetByteCount(text.Substring(0, split)) + sepLength;
            entity.Body = data.Skip(bodyStart).ToArray();
            return entity;
        }

        /// <summary>Splits a multipart body into its parts, returning each part's raw bytes (as signed).</summary>
        public List<byte[]> RawParts()
        {
            var boundary = Parameter("boundary") ?? throw new FormatException("Multipart entity has no boundary.");
            var text = Latin1.GetString(Body);
            var delimiter = "--" + boundary;
            var parts = new List<byte[]>();
            var pos = text.IndexOf(delimiter, StringComparison.Ordinal);
            if (pos < 0) throw new FormatException("Boundary not found in multipart body.");
            while (true)
            {
                var start = text.IndexOf('\n', pos) + 1;
                if (start <= 0) break;
                var next = text.IndexOf("\r\n" + delimiter, start, StringComparison.Ordinal);
                var nextLength = 2;
                if (next < 0)
                {
                    next = text.IndexOf("\n" + delimiter, start, StringComparison.Ordinal);
                    nextLength = 1;
                }
                if (next < 0) break;
                parts.Add(Latin1.GetBytes(text.Substring(start, next - start)));
                pos = next + nextLength;
                if (text.Length >= pos + delimiter.Length + 2 && text.Substring(pos + delimiter.Length, 2) == "--") break;
            }
            return parts;
        }

        public static MimeEntity Multipart(string subtype, string boundary, string extraParameters, params byte[][] parts)
        {
            var entity = new MimeEntity();
            entity.SetHeader("MIME-Version", "1.0");
            entity.ContentType = $"multipart/{subtype}; {(string.IsNullOrEmpty(extraParameters) ? "" : extraParameters + "; ")}boundary=\"{boundary}\"";
            using (var ms = new MemoryStream())
            {
                foreach (var part in parts)
                {
                    var head = Latin1.GetBytes("--" + boundary + "\r\n");
                    ms.Write(head, 0, head.Length);
                    ms.Write(part, 0, part.Length);
                    var crlf = Latin1.GetBytes("\r\n");
                    ms.Write(crlf, 0, crlf.Length);
                }
                var end = Latin1.GetBytes("--" + boundary + "--\r\n");
                ms.Write(end, 0, end.Length);
                entity.Body = ms.ToArray();
            }
            return entity;
        }

        public static string NewBoundary() => "----=_Part_" + Guid.NewGuid().ToString("N").Substring(0, 20);
    }
}
