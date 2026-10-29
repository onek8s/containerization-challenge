using System;
using System.IO;
using System.Threading;

namespace Woodgrove.Statements.Distribution.Delivery
{
    /// <summary>Writes files to UNC shares: temporary name first, then rename, so readers on the share never see half a file.</summary>
    public static class ShareWriter
    {
        public static string Write(string folder, string fileName, byte[] content, bool overwrite = true)
        {
            if (string.IsNullOrWhiteSpace(folder)) throw new ArgumentException("Target folder is empty.");
            Directory.CreateDirectory(folder);
            var target = Path.Combine(folder, fileName);
            var temp = Path.Combine(folder, "~" + Guid.NewGuid().ToString("N").Substring(0, 8) + "_" + fileName + ".tmp");
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    File.WriteAllBytes(temp, content);
                    if (File.Exists(target))
                    {
                        if (!overwrite) target = Unique(target);
                        else File.Delete(target);
                    }
                    File.Move(temp, target);
                    return target;
                }
                catch (IOException) when (attempt < 3)
                {
                    // Share briefly unavailable or the previous file still open by a reader.
                    Thread.Sleep(TimeSpan.FromSeconds(5 * attempt));
                }
                finally
                {
                    if (File.Exists(temp)) TryDelete(temp);
                }
            }
        }

        static string Unique(string path)
        {
            var dir = Path.GetDirectoryName(path);
            var name = Path.GetFileNameWithoutExtension(path);
            var ext = Path.GetExtension(path);
            for (var i = 2; ; i++)
            {
                var candidate = Path.Combine(dir, name + "_" + i + ext);
                if (!File.Exists(candidate)) return candidate;
            }
        }

        static void TryDelete(string path)
        {
            try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
