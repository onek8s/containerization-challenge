using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace Fabrikam.Housing.Documents.Imaging
{
    /// <summary>JPEG thumbnails for photos and scanned images (GDI+).</summary>
    public class Thumbnailer : IThumbnailer
    {
        public const int MaxSize = 240;

        public byte[] Create(byte[] content, string contentType)
        {
            if (contentType == null || !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) return null;
            using (var input = new MemoryStream(content))
            using (var source = Image.FromStream(input))
            {
                Orient(source);
                var scale = Math.Min(1.0, Math.Min((double)MaxSize / source.Width, (double)MaxSize / source.Height));
                var w = Math.Max(1, (int)Math.Round(source.Width * scale));
                var h = Math.Max(1, (int)Math.Round(source.Height * scale));
                using (var thumb = new Bitmap(w, h))
                {
                    using (var g = Graphics.FromImage(thumb))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.SmoothingMode = SmoothingMode.HighQuality;
                        g.Clear(Color.White);
                        g.DrawImage(source, 0, 0, w, h);
                    }
                    var codec = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
                    using (var parameters = new EncoderParameters(1))
                    using (var output = new MemoryStream())
                    {
                        parameters.Param[0] = new EncoderParameter(Encoder.Quality, 80L);
                        thumb.Save(output, codec, parameters);
                        return output.ToArray();
                    }
                }
            }
        }

        /// <summary>Phone photos carry the rotation in EXIF tag 0x0112.</summary>
        private static void Orient(Image image)
        {
            if (!image.PropertyIdList.Contains(0x0112)) return;
            switch (image.GetPropertyItem(0x0112).Value[0])
            {
                case 3: image.RotateFlip(RotateFlipType.Rotate180FlipNone); break;
                case 6: image.RotateFlip(RotateFlipType.Rotate90FlipNone); break;
                case 8: image.RotateFlip(RotateFlipType.Rotate270FlipNone); break;
            }
        }
    }
}
