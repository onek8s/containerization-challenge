using System.IO;
using iTextSharp.text;
using iTextSharp.text.html.simpleparser;
using iTextSharp.text.pdf;

namespace Fabrikam.Housing.Documents.Pdf
{
    /// <summary>HTML to PDF with the iTextSharp HTML worker: tables, paragraphs and basic inline styles only.</summary>
    public class BasicPdfRenderer : IPdfRenderer
    {
        public byte[] RenderHtml(string html, string title)
        {
            using (var output = new MemoryStream())
            {
                var document = new Document(PageSize.A4, 56, 56, 50, 50);
                var writer = PdfWriter.GetInstance(document, output);
                writer.CloseStream = false;
                document.AddTitle(title ?? "Fabrikam Homes");
                document.AddAuthor("Fabrikam Homes");
                document.Open();
                using (var reader = new StringReader(HtmlText.BodyOf(html)))
                {
                    var worker = new HtmlWorker(document);
                    worker.Parse(reader);
                }
                document.Close();
                return output.ToArray();
            }
        }
    }
}
