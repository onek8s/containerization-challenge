using System.IO;
using System.IO.Packaging;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Xps;
using System.Windows.Xps.Packaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StudyData.Documents.Printing;

namespace StudyData.Documents.Wpf;

/// <summary>
/// Lays out casebooks as WPF FixedDocuments from the XAML templates. Page images for the viewer come from
/// RenderTargetBitmap; the archive copy is written as XPS and printed to PDF through the PDF print queue.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WpfCasebookRenderer : ICasebookRenderer
{
    private static readonly Size A4 = new(793.7, 1122.5);

    private readonly WpfRenderThread _thread;
    private readonly PdfPrintQueue _printer;
    private readonly DocumentOptions _options;
    private readonly ILogger<WpfCasebookRenderer> _log;
    private readonly Dictionary<string, string> _templates = new();

    public WpfCasebookRenderer(WpfRenderThread thread, PdfPrintQueue printer, IOptions<DocumentOptions> options, ILogger<WpfCasebookRenderer> log)
    {
        _thread = thread;
        _printer = printer;
        _options = options.Value;
        _log = log;
    }

    public Task<IReadOnlyList<RenderedPage>> RenderPagesAsync(CasebookModel model, double dpi, CancellationToken ct = default) =>
        _thread.InvokeAsync<IReadOnlyList<RenderedPage>>(() =>
        {
            var pages = new List<RenderedPage>();
            foreach (var page in CasebookPages.Layout(model, _options.RowsPerPage))
            {
                var visual = CreatePage(page, model);
                var bitmap = new RenderTargetBitmap((int)(A4.Width * dpi / 96), (int)(A4.Height * dpi / 96), dpi, dpi, PixelFormats.Pbgra32);
                bitmap.Render(visual);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var ms = new MemoryStream();
                encoder.Save(ms);
                pages.Add(new RenderedPage(page.Number, ms.ToArray(), "image/png"));
            }

            return pages;
        }, ct);

    public async Task<string> WriteArchiveAsync(CasebookModel model, string outputPathWithoutExtension, CancellationToken ct = default)
    {
        Directory.CreateDirectory(_options.SpoolFolder);
        var xpsPath = Path.Combine(_options.SpoolFolder, $"{model.StudyCode}_{model.SubjectNumber}_{Guid.NewGuid():N}.xps");
        await _thread.InvokeAsync(() =>
        {
            var document = CreateDocument(model);
            using var xps = new XpsDocument(xpsPath, FileAccess.ReadWrite, CompressionOption.Maximum);
            var writer = XpsDocument.CreateXpsDocumentWriter(xps);
            writer.Write(document);
            return 0;
        }, ct);

        try
        {
            var pdf = outputPathWithoutExtension + ".pdf";
            await _printer.PrintXpsToPdfAsync(xpsPath, pdf, $"{model.StudyCode} {model.SubjectNumber} casebook", ct);
            _log.LogInformation("Casebook {Subject} written to {Pdf}", model.SubjectNumber, pdf);
            return pdf;
        }
        finally
        {
            File.Delete(xpsPath);
        }
    }

    private FixedDocument CreateDocument(CasebookModel model)
    {
        var document = new FixedDocument();
        document.DocumentPaginator.PageSize = A4;
        foreach (var page in CasebookPages.Layout(model, _options.RowsPerPage))
        {
            var fixedPage = new FixedPage { Width = A4.Width, Height = A4.Height };
            fixedPage.Children.Add(CreatePage(page, model));
            var content = new PageContent();
            ((IAddChild)content).AddChild(fixedPage);
            document.Pages.Add(content);
        }

        return document;
    }

    private FrameworkElement CreatePage(CasebookPages.Page page, CasebookModel model)
    {
        var total = CasebookPages.Layout(model, _options.RowsPerPage).Count;
        var label = $"Page {page.Number} of {total}";
        object vm = page switch
        {
            CasebookPages.Cover c => new CoverPageViewModel
            {
                StudyCode = c.Model.StudyCode, StudyTitle = c.Model.StudyTitle, Sponsor = c.Model.Sponsor, SubjectNumber = c.Model.SubjectNumber,
                Site = c.Model.Site, Investigator = c.Model.Investigator, Status = c.Model.Status,
                RandomisationNumber = c.Model.RandomisationNumber ?? "not randomised", FormCount = c.Model.Forms.Count.ToString(),
                Generated = $"Generated {c.Model.GeneratedAt} by {c.Model.GeneratedBy}"
            },
            CasebookPages.FormPage f => new FormPageViewModel
            {
                StudyCode = model.StudyCode, Sponsor = model.Sponsor, SubjectLine = $"Subject {model.SubjectNumber}", SiteLine = model.Site,
                FormTitle = f.Form.FormName + (f.Form.Row > 1 || f.Form.FormCode is "AE" or "CM" ? $" #{f.Form.Row}" : "") + (f.Continued ? " (continued)" : ""),
                VisitTitle = $"{f.Form.VisitName} ({f.Form.VisitCode}){(f.Form.VisitDate is null ? "" : " · " + f.Form.VisitDate)}",
                Status = f.Form.Status,
                SignatureLine = f.Form.SignedBy is null ? "Not signed" : $"Electronically signed by {f.Form.SignedBy} on {f.Form.SignedAt}",
                Footer = $"{_options.Organisation} · {model.GeneratedAt}",
                PageLabel = label,
                Rows = f.Rows.Select((r, i) => new FieldRowViewModel
                {
                    Code = r.Code, Label = r.Label + (r.Unit is null ? "" : $" ({r.Unit})"), ValueText = r.Value ?? "—",
                    Marks = CasebookPages.Marks(r), Shade = i % 2 == 0 ? "White" : "#F7F9FB"
                }).ToList()
            },
            CasebookPages.AuditPage a => new AuditPageViewModel
            {
                Title = a.Title, SubjectLine = $"{model.StudyCode} · Subject {model.SubjectNumber}", PageLabel = label,
                Rows = a.Rows.Select(r => new AuditRowViewModel { At = r.At, User = r.User, Text = r.Text }).ToList()
            },
            _ => throw new InvalidOperationException("Unknown page")
        };

        var template = page switch
        {
            CasebookPages.Cover => "CoverPage",
            CasebookPages.FormPage => "FormPage",
            _ => "AuditPage"
        };

        var element = (FrameworkElement)XamlReader.Parse(Template(template));
        element.DataContext = vm;
        element.Measure(A4);
        element.Arrange(new Rect(A4));
        element.UpdateLayout();
        return element;
    }

    private string Template(string name)
    {
        if (!_templates.TryGetValue(name, out var xaml))
        {
            var resource = typeof(WpfCasebookRenderer).Assembly.GetManifestResourceNames().Single(n => n.EndsWith($".Templates.{name}.xaml", StringComparison.Ordinal));
            using var stream = typeof(WpfCasebookRenderer).Assembly.GetManifestResourceStream(resource)!;
            using var reader = new StreamReader(stream);
            xaml = _templates[name] = reader.ReadToEnd();
        }

        return xaml;
    }
}
