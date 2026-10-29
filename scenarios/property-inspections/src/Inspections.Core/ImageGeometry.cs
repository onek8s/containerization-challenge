using System;

namespace Northwind.Inspections.Core
{
    public enum OrientationFix
    {
        None,
        FlipX,
        Rotate180,
        FlipY,
        Rotate90FlipX,
        Rotate90,
        Rotate270FlipX,
        Rotate270
    }

    public struct Size2
    {
        public Size2(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public int Width { get; }
        public int Height { get; }

        public override string ToString()
        {
            return Width + "x" + Height;
        }
    }

    public static class ImageGeometry
    {
        public const int ExifOrientationTag = 0x0112;
        public const int ExifDateTimeOriginalTag = 0x9003;

        /// <summary>Maps the EXIF orientation value (1-8) to the transform that makes the image upright.</summary>
        public static OrientationFix FromExifOrientation(int orientation)
        {
            switch (orientation)
            {
                case 2: return OrientationFix.FlipX;
                case 3: return OrientationFix.Rotate180;
                case 4: return OrientationFix.FlipY;
                case 5: return OrientationFix.Rotate90FlipX;
                case 6: return OrientationFix.Rotate90;
                case 7: return OrientationFix.Rotate270FlipX;
                case 8: return OrientationFix.Rotate270;
                default: return OrientationFix.None;
            }
        }

        public static bool SwapsAxes(OrientationFix fix)
        {
            return fix == OrientationFix.Rotate90 || fix == OrientationFix.Rotate270
                || fix == OrientationFix.Rotate90FlipX || fix == OrientationFix.Rotate270FlipX;
        }

        /// <summary>Scales down so the longest edge is at most maxEdge. Never scales up.</summary>
        public static Size2 FitWithin(int width, int height, int maxEdge)
        {
            if (width <= 0 || height <= 0)
            {
                throw new ArgumentOutOfRangeException("width", "Image has no size.");
            }
            var longest = Math.Max(width, height);
            if (longest <= maxEdge)
            {
                return new Size2(width, height);
            }
            var ratio = (double)maxEdge / longest;
            return new Size2(Math.Max(1, (int)Math.Round(width * ratio)), Math.Max(1, (int)Math.Round(height * ratio)));
        }

        /// <summary>Contact sheet grid: columns and rows needed for count cells.</summary>
        public static Size2 Grid(int count, int columns)
        {
            if (count <= 0)
            {
                return new Size2(0, 0);
            }
            var cols = Math.Min(count, columns);
            var rows = (count + cols - 1) / cols;
            return new Size2(cols, rows);
        }

        /// <summary>Parses the EXIF DateTimeOriginal ASCII value "yyyy:MM:dd HH:mm:ss".</summary>
        public static DateTime? ParseExifDate(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }
            DateTime parsed;
            if (DateTime.TryParseExact(value.Trim('\0', ' '), "yyyy:MM:dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out parsed))
            {
                return parsed;
            }
            return null;
        }
    }
}
