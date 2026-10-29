using System.Reflection;
using System.Runtime.InteropServices;
using DinkToPdf;

namespace Proseware.ControlTower.Documents.Pdf;

/// <summary>
/// DinkToPdf imports "libwkhtmltox" by name. The library is installed with wkhtmltopdf 0.12.6 (MSVC 2015 build, 64-bit)
/// outside the application folder, so imports are resolved to the configured file.
/// </summary>
internal static class WkhtmltoxLoader
{
    private static readonly object Gate = new();
    private static string? _configuredPath;
    private static bool _resolverSet;

    public static void Configure(string libraryPath)
    {
        lock (Gate)
        {
            _configuredPath = libraryPath;
            if (_resolverSet) return;
            NativeLibrary.SetDllImportResolver(typeof(PdfTools).Assembly, Resolve);
            _resolverSet = true;
        }
    }

    private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!libraryName.Contains("wkhtmltox", StringComparison.OrdinalIgnoreCase)) return IntPtr.Zero;
        var path = _configuredPath;
        if (!string.IsNullOrEmpty(path) && File.Exists(path)) return NativeLibrary.Load(path);
        // Fall back to the default probing (application folder, PATH).
        return NativeLibrary.TryLoad(libraryName, assembly, searchPath, out var handle) ? handle : IntPtr.Zero;
    }
}
