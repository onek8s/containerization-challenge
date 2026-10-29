using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using Wingtip.Invoices.Gateway.Channels;
using Wingtip.Invoices.Infrastructure.Configuration;
using Wingtip.Invoices.Infrastructure.Data;
using Wingtip.Invoices.Infrastructure.Diagnostics;
using Wingtip.Invoices.Infrastructure.Messaging;

namespace Wingtip.Invoices.Gateway
{
    /// <summary>
    /// Takes one file from a channel: registers it, copies the original to the archive share and
    /// queues it for the processor.
    /// </summary>
    public class FileIntake
    {
        private readonly FileRepository files;
        private readonly HubCounters counters;
        private readonly long maxFileSize;

        public FileIntake(FileRepository files, HubCounters counters)
        {
            this.files = files;
            this.counters = counters;
            maxFileSize = HubSettings.GetInt("MaxFileSizeMB", 20) * 1024L * 1024L;
        }

        public void Take(InboundChannel channel, IInboundChannel source, InboundItem item)
        {
            if (item.Size > maxFileSize)
            {
                HubLog.Warning(HubLog.Events.FileDuplicate, "{0} from {1} is {2} bytes, larger than the limit. Moved to rejected.", item.Name, source.Description, item.Size);
                source.Complete(item, false);
                return;
            }

            var data = source.Read(item);
            byte[] hash;
            using (var sha = SHA256.Create())
            {
                hash = sha.ComputeHash(data);
            }

            var fileId = files.Register(channel.ChannelId, item.Name, hash, data.Length);
            if (fileId == null)
            {
                HubLog.Warning(HubLog.Events.FileDuplicate, "{0} from {1} was already received with the same content. Moved to rejected.", item.Name, channel.SupplierCode);
                source.Complete(item, false);
                return;
            }

            var archivePath = Archive(fileId.Value, channel.SupplierCode, item.Name, data);
            files.SetArchived(fileId.Value, archivePath);

            var message = new InboundFileMessage
            {
                FileId = fileId.Value,
                SupplierId = channel.SupplierId,
                SupplierCode = channel.SupplierCode,
                FileName = item.Name,
                ArchivePath = archivePath,
                ReceivedAt = DateTime.Now,
                Content = data.Length <= HubQueues.InlineContentLimit ? data : null
            };
            HubQueues.Send(HubQueues.InboundPath, typeof(InboundFileMessage), message, channel.SupplierCode + " " + item.Name);

            source.Complete(item, true);
            counters.FileReceived();
            HubLog.Info(HubLog.Events.FileReceived, "File {0} {1} from {2} queued ({3} bytes).", fileId.Value, item.Name, channel.SupplierCode, data.Length);
        }

        /// <summary>Copies the original to \\files01\InvoiceArchive$\yyyy\MM\SUPPLIER\&lt;file id&gt;_&lt;name&gt;.</summary>
        private static string Archive(long fileId, string supplierCode, string name, byte[] data)
        {
            var now = DateTime.Now;
            var folder = Path.Combine(HubSettings.ArchiveRoot,
                now.ToString("yyyy", CultureInfo.InvariantCulture),
                now.ToString("MM", CultureInfo.InvariantCulture),
                supplierCode);
            Directory.CreateDirectory(folder);

            var path = Path.Combine(folder, fileId.ToString(CultureInfo.InvariantCulture) + "_" + name);
            var temp = path + ".partial";
            File.WriteAllBytes(temp, data);
            if (File.Exists(path)) File.Delete(path);
            File.Move(temp, path);
            File.SetAttributes(path, FileAttributes.ReadOnly);
            return path;
        }
    }
}
