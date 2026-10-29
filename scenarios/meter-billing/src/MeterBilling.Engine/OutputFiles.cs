using System;
using System.IO;

namespace Coho.Billing.Engine
{
    internal static class OutputFiles
    {
        /// <summary>
        /// Writes the file under a temporary name and renames it when complete, so that the print
        /// vendor and bank transfer jobs never pick up a partial file.
        /// </summary>
        public static string Write(string folder, string fileName, Action<Stream> write)
        {
            Directory.CreateDirectory(folder);
            var target = Path.Combine(folder, fileName);
            var temp = target + ".tmp";

            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                write(stream);
            }

            if (File.Exists(target))
            {
                File.Move(target, target + "." + DateTime.Now.ToString("yyyyMMddHHmmss") + ".old");
            }

            File.Move(temp, target);
            return target;
        }
    }
}
