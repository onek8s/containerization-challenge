using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ProductReturns.Labels.Carriers
{
    /// <summary>Writes a single page A6 PDF with lines of Helvetica text.</summary>
    public static class SimplePdf
    {
        public static byte[] Create(IEnumerable<string> lines)
        {
            var content = new StringBuilder();
            content.Append("BT /F1 10 Tf 20 390 Td 13 TL\n");
            foreach (var line in lines)
            {
                content.Append('(').Append(Escape(line ?? string.Empty)).Append(") Tj T*\n");
            }

            content.Append("ET\n");
            var stream = content.ToString();

            var objects = new List<string>
            {
                "<< /Type /Catalog /Pages 2 0 R >>",
                "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 298 420] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
                "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
                "<< /Length " + Encoding.ASCII.GetByteCount(stream) + " >>\nstream\n" + stream + "endstream"
            };

            using (var ms = new MemoryStream())
            {
                var offsets = new List<long>();
                Write(ms, "%PDF-1.4\n");
                for (var i = 0; i < objects.Count; i++)
                {
                    offsets.Add(ms.Position);
                    Write(ms, (i + 1) + " 0 obj\n" + objects[i] + "\nendobj\n");
                }

                var xref = ms.Position;
                Write(ms, "xref\n0 " + (objects.Count + 1) + "\n0000000000 65535 f \n");
                foreach (var offset in offsets)
                {
                    Write(ms, offset.ToString("D10") + " 00000 n \n");
                }

                Write(ms, "trailer\n<< /Size " + (objects.Count + 1) + " /Root 1 0 R >>\nstartxref\n" + xref + "\n%%EOF\n");
                return ms.ToArray();
            }
        }

        private static string Escape(string text)
        {
            var sb = new StringBuilder(text.Length);
            foreach (var c in text)
            {
                if (c == '(' || c == ')' || c == '\\')
                {
                    sb.Append('\\');
                }

                sb.Append(c > 126 ? '?' : c);
            }

            return sb.ToString();
        }

        private static void Write(Stream stream, string text)
        {
            var bytes = Encoding.ASCII.GetBytes(text);
            stream.Write(bytes, 0, bytes.Length);
        }
    }
}
