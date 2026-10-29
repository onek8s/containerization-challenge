using System.Runtime.Versioning;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;
using Windows.Storage.Streams;

namespace CityPermits.MailIntake;

public sealed record OcrWord(string Text, double X, double Y, double Width, double Height);
public sealed record OcrPage(int Number, int Width, int Height, string Text, IReadOnlyList<OcrWord> Words);

public interface IPageReader
{
    Task<IReadOnlyList<OcrPage>> ReadAsync(string path, CancellationToken ct);
}

/// <summary>
/// Reads multi-page TIFFs from the mailroom scanners with the Windows imaging decoder and the
/// built-in Windows OCR engine (en-GB language pack must be installed on the server).
/// </summary>
[SupportedOSPlatform("windows10.0.17763.0")]
public sealed class WindowsOcrPageReader : IPageReader
{
    private readonly OcrEngine _engine;

    public WindowsOcrPageReader(IConfiguration cfg)
    {
        var tag = cfg["Ocr:Language"] ?? "en-GB";
        _engine = OcrEngine.TryCreateFromLanguage(new Language(tag))
            ?? throw new InvalidOperationException($"OCR language {tag} is not installed (Add-WindowsCapability -Online -Name Language.OCR~~~{tag}~0.0.1.0).");
    }

    public async Task<IReadOnlyList<OcrPage>> ReadAsync(string path, CancellationToken ct)
    {
        var file = await StorageFile.GetFileFromPathAsync(Path.GetFullPath(path));
        using IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read);
        var decoder = await BitmapDecoder.CreateAsync(BitmapDecoder.TiffDecoderId, stream);
        var pages = new List<OcrPage>();
        for (uint i = 0; i < decoder.FrameCount; i++)
        {
            ct.ThrowIfCancellationRequested();
            var frame = await decoder.GetFrameAsync(i);
            using var bitmap = await frame.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
            using var scaled = bitmap.PixelWidth > OcrEngine.MaxImageDimension || bitmap.PixelHeight > OcrEngine.MaxImageDimension
                ? await Downscale(frame, bitmap) : null;
            var result = await _engine.RecognizeAsync(scaled ?? bitmap);
            var words = result.Lines.SelectMany(l => l.Words)
                .Select(w => new OcrWord(w.Text, w.BoundingRect.X, w.BoundingRect.Y, w.BoundingRect.Width, w.BoundingRect.Height)).ToList();
            pages.Add(new OcrPage((int)i + 1, (scaled ?? bitmap).PixelWidth, (scaled ?? bitmap).PixelHeight,
                string.Join("\n", result.Lines.Select(l => l.Text)), words));
        }
        return pages;
    }

    private static async Task<SoftwareBitmap> Downscale(BitmapFrame frame, SoftwareBitmap bitmap)
    {
        var factor = (double)OcrEngine.MaxImageDimension / Math.Max(bitmap.PixelWidth, bitmap.PixelHeight);
        var transform = new BitmapTransform
        {
            ScaledWidth = (uint)(bitmap.PixelWidth * factor),
            ScaledHeight = (uint)(bitmap.PixelHeight * factor),
            InterpolationMode = BitmapInterpolationMode.Fant
        };
        return await frame.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, transform,
            ExifOrientationMode.RespectExifOrientation, ColorManagementMode.DoNotColorManage);
    }
}

/// <summary>Development: reads the text of a scan from a .txt file next to it.</summary>
public sealed class SidecarTextPageReader : IPageReader
{
    public Task<IReadOnlyList<OcrPage>> ReadAsync(string path, CancellationToken ct)
    {
        var txt = Path.ChangeExtension(path, ".txt");
        var pages = File.Exists(txt)
            ? File.ReadAllText(txt).Split('\f').Select((t, i) => new OcrPage(i + 1, 2480, 3508, t.Trim(), Array.Empty<OcrWord>())).ToList()
            : new List<OcrPage>();
        return Task.FromResult<IReadOnlyList<OcrPage>>(pages);
    }
}
