using System;
using System.Collections;
using System.Text;
using iTextSharp.text.pdf;
using Microsoft.Extensions.Logging;

namespace Fabrikam.Housing.Documents.Search
{
    /// <summary>
    /// Text for the full text index. PDFs are read by walking the page content streams for text operators,
    /// which works for generated letters and certificates; scanned images have no text and are indexed by title only.
    /// </summary>
    public class TextExtractor : ITextExtractor
    {
        private readonly ILogger<TextExtractor> _logger;

        public TextExtractor(ILogger<TextExtractor> logger)
        {
            _logger = logger;
        }

        public string Extract(byte[] content, string contentType, string fileName)
        {
            try
            {
                var type = (contentType ?? "").ToLowerInvariant();
                if (type.StartsWith("text/html") || fileName.EndsWith(".htm", StringComparison.OrdinalIgnoreCase) || fileName.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
                    return HtmlText.ToText(Encoding.UTF8.GetString(content));
                if (type.StartsWith("text/") || fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) || fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                    return Encoding.UTF8.GetString(content);
                if (type == "application/pdf" || fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                    return Pdf(content);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not extract text from {File}", fileName);
            }
            return "";
        }

        public static string Pdf(byte[] content)
        {
            var reader = new PdfReader(content);
            var sb = new StringBuilder();
            try
            {
                for (var page = 1; page <= reader.NumberOfPages; page++)
                {
                    var parser = new PdfContentParser(new PrTokeniser(reader.GetPageContent(page)));
                    var operands = new ArrayList();
                    while (parser.Parse(operands).Count > 0)
                    {
                        var op = operands[operands.Count - 1].ToString();
                        if (op == "Tj" || op == "'" || op == "\"")
                        {
                            if (operands[operands.Count - 2] is PdfString s) sb.Append(s.ToUnicodeString());
                        }
                        else if (op == "TJ" && operands[0] is PdfArray array)
                        {
                            foreach (var item in array.ArrayList)
                                if (item is PdfString part) sb.Append(part.ToUnicodeString());
                        }
                        else if (op == "ET" || op == "T*" || op == "Td" || op == "TD")
                        {
                            sb.Append(' ');
                        }
                    }
                    sb.AppendLine();
                }
            }
            finally
            {
                reader.Close();
            }
            return System.Text.RegularExpressions.Regex.Replace(sb.ToString(), @"[ \t]+", " ").Trim();
        }
    }
}
