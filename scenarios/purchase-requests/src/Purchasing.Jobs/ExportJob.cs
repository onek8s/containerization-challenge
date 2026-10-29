using System;
using System.Configuration;
using System.IO;
using System.Linq;
using Contoso.Purchasing.Core;
using Contoso.Purchasing.Data;

namespace Contoso.Purchasing.Jobs
{
    /// <summary>
    /// Writes new purchase orders to the ERP inbound folder. The file is written under a temporary
    /// name first because the ERP interface picks up every *.csv file as soon as it appears.
    /// </summary>
    internal class ExportJob
    {
        private readonly Database _db;

        public ExportJob(Database db)
        {
            _db = db;
        }

        public int Run(DateTime now)
        {
            var orders = new OrderRepository(_db);
            var pending = orders.NotExported();
            if (pending.Count == 0)
            {
                JobLog.Info("No new purchase orders to export.");
                return 0;
            }

            var export = new ErpOrderExport();
            var folder = ConfigurationManager.AppSettings["Export.Folder"];
            var archive = ConfigurationManager.AppSettings["Export.ArchiveFolder"];
            var fileName = export.FileName(now);
            var tempPath = Path.Combine(folder, fileName + ".tmp");
            var finalPath = Path.Combine(folder, fileName);

            using (var writer = new StreamWriter(tempPath, false, ErpOrderExport.FileEncoding))
            {
                export.Write(writer, pending);
            }

            if (!string.IsNullOrEmpty(archive))
            {
                var archiveFolder = Path.Combine(archive, now.ToString("yyyy"));
                Directory.CreateDirectory(archiveFolder);
                File.Copy(tempPath, Path.Combine(archiveFolder, fileName), true);
            }

            File.Move(tempPath, finalPath);
            orders.MarkExported(pending.Select(o => o.Id), fileName, now);

            JobLog.Info("Exported " + pending.Count + " purchase order(s) to " + finalPath + ": " + string.Join(", ", pending.Select(o => o.PoNumber)));
            return 0;
        }
    }
}
