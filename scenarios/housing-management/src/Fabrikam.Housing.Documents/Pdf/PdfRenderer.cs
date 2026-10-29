using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using DinkToPdf;
using DinkToPdf.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fabrikam.Housing.Documents.Pdf
{
    /// <summary>
    /// Renders letters from HTML. Uses wkhtmltopdf (libwkhtmltox.dll in the native folder next to the
    /// application) for full CSS support, and the iTextSharp HTML worker when that is not configured or not deployed.
    /// </summary>
    public class PdfRenderer : IPdfRenderer
    {
        private static readonly object LoadGate = new object();
        private static IConverter _converter;
        private static bool _nativeChecked;

        private readonly DocumentsOptions _options;
        private readonly ILogger<PdfRenderer> _logger;
        private readonly BasicPdfRenderer _basic = new BasicPdfRenderer();

        public PdfRenderer(IOptions<DocumentsOptions> options, ILogger<PdfRenderer> logger)
        {
            _options = options.Value;
            _logger = logger;
        }

        public byte[] RenderHtml(string html, string title)
        {
            var converter = string.Equals(_options.PdfEngine, "Wkhtmltopdf", StringComparison.OrdinalIgnoreCase) ? Converter() : null;
            if (converter == null) return _basic.RenderHtml(html, title);

            var document = new HtmlToPdfDocument
            {
                GlobalSettings =
                {
                    ColorMode = ColorMode.Color,
                    Orientation = Orientation.Portrait,
                    PaperSize = PaperKind.A4,
                    Margins = new MarginSettings { Top = 18, Bottom = 18, Left = 20, Right = 20 },
                    DocumentTitle = title
                },
                Objects =
                {
                    new ObjectSettings
                    {
                        HtmlContent = html,
                        WebSettings = { DefaultEncoding = "utf-8", LoadImages = true },
                        FooterSettings = { FontName = "Arial", FontSize = 8, Center = "Fabrikam Homes, registered society no. 30112R, regulated by the Regulator of Social Housing (L4412)" }
                    }
                }
            };
            return converter.Convert(document);
        }

        private IConverter Converter()
        {
            lock (LoadGate)
            {
                if (_nativeChecked) return _converter;
                _nativeChecked = true;
                var path = Path.Combine(AppContext.BaseDirectory, _options.NativeLibraryFolder, "libwkhtmltox.dll");
                if (!File.Exists(path))
                {
                    _logger.LogWarning("{Path} not found, letters are rendered with the basic PDF engine", path);
                    return null;
                }
                try
                {
                    new CustomAssemblyLoadContext().LoadUnmanagedLibrary(path);
                    _converter = new SynchronizedConverter(new PdfTools());
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not load {Path}, letters are rendered with the basic PDF engine", path);
                }
                return _converter;
            }
        }

        private class CustomAssemblyLoadContext : AssemblyLoadContext
        {
            public IntPtr LoadUnmanagedLibrary(string absolutePath) => LoadUnmanagedDll(absolutePath);

            protected override IntPtr LoadUnmanagedDll(string unmanagedDllName) => LoadUnmanagedDllFromPath(unmanagedDllName);

            protected override Assembly Load(AssemblyName assemblyName) => throw new NotImplementedException();
        }
    }
}
