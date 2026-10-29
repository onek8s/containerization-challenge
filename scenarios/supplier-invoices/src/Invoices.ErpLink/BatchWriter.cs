using System;
using System.IO;
using System.Linq;
using System.Text;
using Wingtip.Invoices.Core.Erp;
using Wingtip.Invoices.Infrastructure.Configuration;
using Wingtip.Invoices.Infrastructure.Data;
using Wingtip.Invoices.Infrastructure.Diagnostics;

namespace Wingtip.Invoices.ErpLink
{
    /// <summary>
    /// Writes pending invoices to APIMP files in the ERP import folder. The ERP job APIMP01 picks
    /// up *.dat files every 30 minutes, so files are written under a temporary name and renamed.
    /// </summary>
    public class BatchWriter
    {
        private readonly PostingRepository postings;
        private readonly HubCounters counters;
        private DateTime lastBatch = DateTime.MinValue;

        public BatchWriter(PostingRepository postings, HubCounters counters)
        {
            this.postings = postings;
            this.counters = counters;
        }

        public void Run()
        {
            foreach (var batch in postings.GetUnwrittenBatches()) Write(batch);

            var pending = postings.CountPending();
            if (pending == 0) return;

            var maxSize = HubSettings.GetInt("MaxBatchSize", 200);
            var interval = TimeSpan.FromMinutes(HubSettings.GetInt("BatchIntervalMinutes", 15));
            if (pending < maxSize && DateTime.Now - lastBatch < interval) return;

            var created = postings.CreateBatch(maxSize);
            if (created != null) Write(created);
        }

        private void Write(PostingBatch batch)
        {
            var folder = HubSettings.Require("ErpImportFolder");
            var encoding = Encoding.GetEncoding(HubSettings.GetInt("ErpCodePage", 850));
            var name = ErpImportFile.FileName(batch.BatchId, batch.CreatedAt);
            var temp = Path.Combine(folder, Path.ChangeExtension(name, ".tmp"));
            var target = Path.Combine(folder, name);

            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                ErpImportFile.Write(stream, encoding, batch.BatchId, HubSettings.Company, batch.CreatedAt, batch.Invoices);
            }
            if (File.Exists(target)) File.Delete(target);
            File.Move(temp, target);

            var gross = batch.Invoices.Sum(i => i.GrossAmount);
            postings.MarkWritten(batch.BatchId, name, gross);
            lastBatch = DateTime.Now;
            counters.Posted(batch.Invoices.Count);
            HubLog.Info(HubLog.Events.BatchWritten, "Batch {0} with {1} invoices ({2:N2}) written to {3}.", batch.BatchId, batch.Invoices.Count, gross, target);
        }
    }
}
