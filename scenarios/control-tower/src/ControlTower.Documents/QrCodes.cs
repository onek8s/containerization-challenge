using QRCoder;

namespace Proseware.ControlTower.Documents;

public static class QrCodes
{
    /// <summary>PNG QR code as a data URI for the HTML templates.</summary>
    public static string PngDataUri(string text, int pixelsPerModule = 4)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(data).GetGraphic(pixelsPerModule);
        return "data:image/png;base64," + Convert.ToBase64String(png);
    }
}
