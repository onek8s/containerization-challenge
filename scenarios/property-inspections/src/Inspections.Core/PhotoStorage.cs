using System;
using System.IO;
using System.Linq;

namespace Northwind.Inspections.Core
{
    /// <summary>
    /// Folder layout on the photo share: {year}\{claim number}\{photo id}.jpg and thumbs\{photo id}.jpg.
    /// Paths stored in the database are relative to the share root.
    /// </summary>
    public static class PhotoStorage
    {
        public static readonly string[] AcceptedExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".tif", ".tiff", ".gif" };

        public const long MaxUploadBytes = 15 * 1024 * 1024;

        public static string RelativePath(string claimNumber, Guid photoId)
        {
            var claim = ClaimNumber.Normalize(claimNumber);
            return ClaimNumber.Year(claim) + "\\" + claim + "\\" + photoId.ToString("N") + ".jpg";
        }

        public static string RelativeThumbnailPath(string claimNumber, Guid photoId)
        {
            var claim = ClaimNumber.Normalize(claimNumber);
            return ClaimNumber.Year(claim) + "\\" + claim + "\\thumbs\\" + photoId.ToString("N") + ".jpg";
        }

        public static string Combine(string shareRoot, string relativePath)
        {
            if (string.IsNullOrEmpty(shareRoot))
            {
                throw new ArgumentException("Share root is not configured.", "shareRoot");
            }
            if (relativePath.Contains("..") || relativePath.StartsWith("\\", StringComparison.Ordinal) || relativePath.Contains(":"))
            {
                throw new ArgumentException("Invalid relative path.", "relativePath");
            }
            return shareRoot.TrimEnd('\\') + "\\" + relativePath;
        }

        public static bool IsAcceptedFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return false;
            }
            var ext = Path.GetExtension(fileName);
            return AcceptedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
        }

        public static string SafeFileName(string fileName)
        {
            // Strip any client path; a drive-like prefix such as "a:" is kept and sanitized rather than dropped.
            var name = fileName ?? string.Empty;
            name = name.Substring(name.LastIndexOfAny(new[] { '\\', '/' }) + 1);
            foreach (var c in Path.GetInvalidFileNameChars().Concat(new[] { '\\', '/', ':' }))
            {
                name = name.Replace(c, '_');
            }
            return name.Length > 120 ? name.Substring(name.Length - 120) : name;
        }
    }
}
