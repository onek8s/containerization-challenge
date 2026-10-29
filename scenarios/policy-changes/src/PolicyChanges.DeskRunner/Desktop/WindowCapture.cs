using PolicyChanges.Core.Flows;
using PolicyChanges.Core.Imaging;
using PolicyChanges.DeskRunner.Native;

namespace PolicyChanges.DeskRunner.Desktop;

internal static class WindowCapture
{
    /// <summary>Renders the window with PrintWindow and returns it as PNG.</summary>
    public static byte[] Png(IntPtr hwnd)
    {
        if (!User32.GetWindowRect(hwnd, out var rect)) throw new DesktopException("Window is gone.");
        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        if (width <= 0 || height <= 0) throw new DesktopException("Window has no visible area.");

        var screen = User32.GetDC(IntPtr.Zero);
        var memory = Gdi32.CreateCompatibleDC(screen);
        var bitmap = Gdi32.CreateCompatibleBitmap(screen, width, height);
        var old = Gdi32.SelectObject(memory, bitmap);
        try
        {
            if (!User32.PrintWindow(hwnd, memory, User32.PW_RENDERFULLCONTENT))
                throw new DesktopException("PrintWindow failed.");

            var header = new Gdi32.BITMAPINFOHEADER
            {
                Size = 40,
                Width = width,
                Height = -height, // top-down rows
                Planes = 1,
                BitCount = 32,
                Compression = Gdi32.BI_RGB,
            };
            var pixels = new byte[width * height * 4];
            Gdi32.SelectObject(memory, old);
            if (Gdi32.GetDIBits(memory, bitmap, 0, (uint)height, pixels, ref header, Gdi32.DIB_RGB_COLORS) == 0)
                throw new DesktopException("GetDIBits failed.");
            return PngEncoder.FromBgra(pixels, width, height);
        }
        finally
        {
            Gdi32.DeleteObject(bitmap);
            Gdi32.DeleteDC(memory);
            User32.ReleaseDC(IntPtr.Zero, screen);
        }
    }
}
