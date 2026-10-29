using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Wingtip.Invoices.Gateway.Channels
{
    /// <summary>
    /// Supplier folder on the drop share, e.g. \\files01.corp.local\InvoiceDrop$\LOS. Suppliers with
    /// a VPN connection and the scanning service copy files here; processed files are moved to
    /// processed\yyyyMM and rejected files to rejected.
    /// </summary>
    public class ShareChannel : IInboundChannel
    {
        private readonly string folder;
        private readonly string[] patterns;
        private readonly TimeSpan settleTime;

        public ShareChannel(string folder, string filePattern, TimeSpan settleTime)
        {
            this.folder = folder;
            patterns = (filePattern ?? "*.*").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            this.settleTime = settleTime;
        }

        public string Description
        {
            get { return folder; }
        }

        public IList<InboundItem> List()
        {
            if (!Directory.Exists(folder)) throw new DirectoryNotFoundException("Drop folder " + folder + " is not reachable.");

            var cutoff = DateTime.UtcNow - settleTime;
            return patterns
                .SelectMany(p => new DirectoryInfo(folder).EnumerateFiles(p.Trim(), SearchOption.TopDirectoryOnly))
                .GroupBy(f => f.FullName, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .Where(f => f.LastWriteTimeUtc < cutoff && (f.Attributes & (FileAttributes.Hidden | FileAttributes.Temporary)) == 0)
                .Where(f => !f.Name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) && !f.Name.StartsWith("~"))
                .OrderBy(f => f.LastWriteTimeUtc)
                .Select(f => new InboundItem { Name = f.Name, Path = f.FullName, Size = f.Length, Modified = f.LastWriteTime })
                .ToList();
        }

        public byte[] Read(InboundItem item)
        {
            // Exclusive open: a supplier still copying the file keeps it locked.
            using (var stream = new FileStream(item.Path, FileMode.Open, FileAccess.Read, FileShare.None))
            using (var ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                return ms.ToArray();
            }
        }

        public void Complete(InboundItem item, bool accepted)
        {
            var target = accepted
                ? Path.Combine(folder, "processed", DateTime.Now.ToString("yyyyMM", CultureInfo.InvariantCulture))
                : Path.Combine(folder, "rejected");
            Directory.CreateDirectory(target);

            var destination = Path.Combine(target, item.Name);
            if (File.Exists(destination))
            {
                destination = Path.Combine(target, Path.GetFileNameWithoutExtension(item.Name) + "_" +
                    DateTime.Now.ToString("HHmmss", CultureInfo.InvariantCulture) + Path.GetExtension(item.Name));
            }
            File.Move(item.Path, destination);
        }
    }
}
