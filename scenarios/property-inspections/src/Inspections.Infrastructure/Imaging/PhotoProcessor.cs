using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Text;
using Northwind.Inspections.Core;

namespace Northwind.Inspections.Infrastructure.Imaging
{
    public sealed class ProcessedPhoto
    {
        public byte[] Full { get; set; }
        public byte[] Thumbnail { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public DateTime? Taken { get; set; }
    }

    public class InvalidPhotoException : Exception
    {
        public InvalidPhotoException(string message, Exception inner) : base(message, inner)
        {
        }
    }

    /// <summary>
    /// Turns an uploaded camera image into the stored JPEG: upright (EXIF orientation), scaled down, watermarked
    /// with the claim number, plus a thumbnail. Uses GDI+.
    /// </summary>
    public sealed class PhotoProcessor
    {
        private static readonly ImageCodecInfo JpegCodec = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);

        private readonly int maxEdge;
        private readonly int thumbnailEdge;
        private readonly long quality;
        private readonly string fontFamily;

        public PhotoProcessor(int maxEdge, int thumbnailEdge, long quality, string fontFamily)
        {
            this.maxEdge = maxEdge;
            this.thumbnailEdge = thumbnailEdge;
            this.quality = quality;
            this.fontFamily = fontFamily;
        }

        public ProcessedPhoto Process(byte[] content, string claimNumber, int sequence, string adjusterName, DateTime uploadedLocal)
        {
            Image source;
            try
            {
                source = Image.FromStream(new MemoryStream(content), true, true);
            }
            catch (ArgumentException ex)
            {
                throw new InvalidPhotoException("The file is not a supported image.", ex);
            }
            catch (OutOfMemoryException ex)
            {
                // GDI+ reports corrupt or unsupported images as OutOfMemoryException.
                throw new InvalidPhotoException("The image could not be decoded.", ex);
            }

            using (source)
            {
                var taken = ReadTaken(source);
                ApplyOrientation(source);

                var size = ImageGeometry.FitWithin(source.Width, source.Height, maxEdge);
                using (var full = Resize(source, size.Width, size.Height))
                {
                    DrawWatermark(full, Watermark.Text(claimNumber, taken ?? uploadedLocal, sequence, adjusterName));
                    var thumbSize = ImageGeometry.FitWithin(full.Width, full.Height, thumbnailEdge);
                    using (var thumb = Resize(full, thumbSize.Width, thumbSize.Height))
                    {
                        return new ProcessedPhoto
                        {
                            Full = EncodeJpeg(full, quality),
                            Thumbnail = EncodeJpeg(thumb, 80),
                            Width = full.Width,
                            Height = full.Height,
                            Taken = taken
                        };
                    }
                }
            }
        }

        public static byte[] EncodeJpeg(Image image, long jpegQuality)
        {
            using (var parameters = new EncoderParameters(1))
            using (var ms = new MemoryStream())
            {
                parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, jpegQuality);
                image.Save(ms, JpegCodec, parameters);
                return ms.ToArray();
            }
        }

        public static Bitmap Resize(Image source, int width, int height)
        {
            var target = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            target.SetResolution(96, 96);
            using (var g = Graphics.FromImage(target))
            using (var attributes = new ImageAttributes())
            {
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.White);
                attributes.SetWrapMode(WrapMode.TileFlipXY);
                g.DrawImage(source, new Rectangle(0, 0, width, height), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel, attributes);
            }
            return target;
        }

        private void DrawWatermark(Bitmap image, string text)
        {
            using (var g = Graphics.FromImage(image))
            {
                var fontSize = Math.Max(9f, image.Height / 48f);
                using (var font = new Font(fontFamily, fontSize, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var band = new SolidBrush(Color.FromArgb(140, 0, 0, 0)))
                using (var brush = new SolidBrush(Color.FromArgb(230, 255, 255, 255)))
                {
                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    var measured = g.MeasureString(text, font);
                    var bandHeight = (int)Math.Ceiling(measured.Height * 1.4f);
                    g.FillRectangle(band, 0, image.Height - bandHeight, image.Width, bandHeight);
                    g.DrawString(text, font, brush, fontSize * 0.6f, image.Height - bandHeight + (bandHeight - measured.Height) / 2f);
                }
            }
        }

        private static void ApplyOrientation(Image image)
        {
            if (!image.PropertyIdList.Contains(ImageGeometry.ExifOrientationTag))
            {
                return;
            }
            var item = image.GetPropertyItem(ImageGeometry.ExifOrientationTag);
            var fix = ImageGeometry.FromExifOrientation(BitConverter.ToUInt16(item.Value, 0));
            if (fix != OrientationFix.None)
            {
                image.RotateFlip(ToRotateFlip(fix));
            }
            image.RemovePropertyItem(ImageGeometry.ExifOrientationTag);
        }

        private static DateTime? ReadTaken(Image image)
        {
            if (!image.PropertyIdList.Contains(ImageGeometry.ExifDateTimeOriginalTag))
            {
                return null;
            }
            var item = image.GetPropertyItem(ImageGeometry.ExifDateTimeOriginalTag);
            return ImageGeometry.ParseExifDate(Encoding.ASCII.GetString(item.Value));
        }

        private static RotateFlipType ToRotateFlip(OrientationFix fix)
        {
            switch (fix)
            {
                case OrientationFix.FlipX: return RotateFlipType.RotateNoneFlipX;
                case OrientationFix.Rotate180: return RotateFlipType.Rotate180FlipNone;
                case OrientationFix.FlipY: return RotateFlipType.RotateNoneFlipY;
                case OrientationFix.Rotate90FlipX: return RotateFlipType.Rotate90FlipX;
                case OrientationFix.Rotate90: return RotateFlipType.Rotate90FlipNone;
                case OrientationFix.Rotate270FlipX: return RotateFlipType.Rotate270FlipX;
                case OrientationFix.Rotate270: return RotateFlipType.Rotate270FlipNone;
                default: return RotateFlipType.RotateNoneFlipNone;
            }
        }
    }
}
