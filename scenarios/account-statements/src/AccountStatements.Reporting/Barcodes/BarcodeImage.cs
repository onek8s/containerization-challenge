using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Woodgrove.Statements.Core.Payments;

namespace Woodgrove.Statements.Reporting.Barcodes
{
    /// <summary>Draws the bank barcode as a 1-bit style PNG for the payment slip (300 dpi, 0.25 mm module).</summary>
    public static class BarcodeImage
    {
        public static byte[] Png(string digits, int moduleWidth = 3, int height = 130)
        {
            var modules = Code128.Modules(digits);
            var width = 0;
            foreach (var m in modules) width += m * moduleWidth;

            using (var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb))
            {
                bitmap.SetResolution(300, 300);
                using (var g = Graphics.FromImage(bitmap))
                {
                    g.Clear(Color.White);
                    var x = 0;
                    var bar = false;
                    foreach (var m in modules)
                    {
                        var w = m * moduleWidth;
                        if (bar) g.FillRectangle(Brushes.Black, x, 0, w, height);
                        x += w;
                        bar = !bar;
                    }
                }
                using (var ms = new MemoryStream())
                {
                    bitmap.Save(ms, ImageFormat.Png);
                    return ms.ToArray();
                }
            }
        }
    }
}
