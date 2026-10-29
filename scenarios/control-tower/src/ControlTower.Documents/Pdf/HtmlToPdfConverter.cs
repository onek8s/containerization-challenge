using DinkToPdf;
using DinkToPdf.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Proseware.ControlTower.Documents.Pdf;

public interface IHtmlToPdfConverter
{
    byte[] Convert(string html, string title, bool landscape = false);
}

/// <summary>
/// wkhtmltopdf through DinkToPdf. The native library is not thread safe; SynchronizedConverter runs every
/// conversion on one dedicated thread, so the converter must be a singleton for the whole process.
/// </summary>
public sealed class WkHtmlToPdfConverter : IHtmlToPdfConverter, IDisposable
{
    private readonly IConverter _converter;
    private readonly ILogger<WkHtmlToPdfConverter> _logger;

    public WkHtmlToPdfConverter(IOptions<DocumentOptions> options, ILogger<WkHtmlToPdfConverter> logger)
    {
        _logger = logger;
        WkhtmltoxLoader.Configure(options.Value.WkhtmltoxPath);
        _converter = new SynchronizedConverter(new PdfTools());
        _converter.Error += (_, e) => _logger.LogError("wkhtmltopdf: {Message}", e.Message);
        _converter.Warning += (_, e) => _logger.LogDebug("wkhtmltopdf: {Message}", e.Message);
    }

    public byte[] Convert(string html, string title, bool landscape = false)
    {
        var document = new HtmlToPdfDocument
        {
            GlobalSettings =
            {
                ColorMode = ColorMode.Color,
                Orientation = landscape ? Orientation.Landscape : Orientation.Portrait,
                PaperSize = PaperKind.A4,
                Margins = new MarginSettings { Top = 12, Bottom = 14, Left = 12, Right = 12, Unit = Unit.Millimeters },
                DocumentTitle = title,
                DPI = 300
            },
            Objects =
            {
                new ObjectSettings
                {
                    HtmlContent = html,
                    PagesCount = true,
                    WebSettings = { DefaultEncoding = "utf-8", LoadImages = true, EnableJavascript = false, PrintMediaType = true },
                    FooterSettings = { FontName = "Arial", FontSize = 7, Right = "[page] / [toPage]", Left = title, Spacing = 3 }
                }
            }
        };
        var started = DateTime.UtcNow;
        var pdf = _converter.Convert(document);
        if (pdf is null || pdf.Length == 0)
            throw new InvalidOperationException($"wkhtmltopdf returned no output for '{title}'.");
        _logger.LogDebug("Rendered {Title} ({Bytes} bytes) in {Ms} ms", title, pdf.Length, (DateTime.UtcNow - started).TotalMilliseconds);
        return pdf;
    }

    public void Dispose() => (_converter as IDisposable)?.Dispose();
}
