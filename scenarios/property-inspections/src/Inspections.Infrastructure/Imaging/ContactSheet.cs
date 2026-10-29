using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using Northwind.Inspections.Core;

namespace Northwind.Inspections.Infrastructure.Imaging
{
    /// <summary>A single JPEG with all thumbnails of an inspection in a grid, used in report e-mails and by partners.</summary>
    public static class ContactSheet
    {
        private const int Columns = 4;
        private const int Cell = 340;
        private const int CaptionHeight = 38;
        private const int HeaderHeight = 56;

        public static byte[] Render(Inspection inspection, IList<KeyValuePair<Photo, byte[]>> thumbnails, string fontFamily)
        {
            var grid = ImageGeometry.Grid(Math.Max(1, thumbnails.Count), Columns);
            var width = grid.Width * Cell;
            var height = HeaderHeight + grid.Height * (Cell + CaptionHeight);

            using (var sheet = new Bitmap(width, height))
            using (var g = Graphics.FromImage(sheet))
            using (var titleFont = new Font(fontFamily, 18f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var captionFont = new Font(fontFamily, 12f, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var format = new StringFormat { Alignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter })
            {
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                g.Clear(Color.White);
                g.DrawString(inspection.ClaimNumber + "  " + inspection.PropertyAddress, titleFont, Brushes.Black, 12, 8);
                g.DrawString(inspection.InspectedOn.ToString("dd/MM/yyyy") + "  " + inspection.AdjusterName, captionFont, Brushes.DimGray, 12, 32);

                for (var i = 0; i < thumbnails.Count; i++)
                {
                    var x = (i % Columns) * Cell;
                    var y = HeaderHeight + (i / Columns) * (Cell + CaptionHeight);
                    using (var thumb = Image.FromStream(new MemoryStream(thumbnails[i].Value)))
                    {
                        var size = ImageGeometry.FitWithin(thumb.Width, thumb.Height, Cell - 20);
                        g.DrawImage(thumb, x + (Cell - size.Width) / 2, y + (Cell - size.Height) / 2, size.Width, size.Height);
                    }
                    var photo = thumbnails[i].Key;
                    var caption = photo.Sequence + ". " + Watermark.Caption(string.IsNullOrWhiteSpace(photo.Caption) ? photo.OriginalFileName : photo.Caption, 48);
                    g.DrawString(caption, captionFont, Brushes.Black, new RectangleF(x + 4, y + Cell, Cell - 8, CaptionHeight), format);
                }

                return PhotoProcessor.EncodeJpeg(sheet, 82);
            }
        }
    }
}
