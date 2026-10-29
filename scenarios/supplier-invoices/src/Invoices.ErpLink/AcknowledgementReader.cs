using System;
using System.IO;
using System.Text;
using Wingtip.Invoices.Core.Erp;
using Wingtip.Invoices.Infrastructure.Configuration;
using Wingtip.Invoices.Infrastructure.Data;
using Wingtip.Invoices.Infrastructure.Diagnostics;

namespace Wingtip.Invoices.ErpLink
{
    /// <summary>Reads APACK result files written by the ERP import job and updates the invoices.</summary>
    public class AcknowledgementReader
    {
        private readonly PostingRepository postings;

        public AcknowledgementReader(PostingRepository postings)
        {
            this.postings = postings;
        }

        public void Run()
        {
            var folder = HubSettings.Require("ErpExportFolder");
            var processed = Path.Combine(folder, "processed");
            var encoding = Encoding.GetEncoding(HubSettings.GetInt("ErpCodePage", 850));

            foreach (var path in Directory.GetFiles(folder, "APACK_*.dat"))
            {
                var name = Path.GetFileName(path);
                int accepted = 0, rejected = 0;
                using (var reader = new StreamReader(path, encoding))
                {
                    foreach (var ack in ErpAcknowledgement.Parse(reader))
                    {
                        postings.ApplyAcknowledgement(name, ack);
                        if (ack.Accepted)
                        {
                            accepted++;
                        }
                        else
                        {
                            rejected++;
                            HubLog.Warning(HubLog.Events.PostingRejected, "ERP rejected invoice {0}: {1}", ack.InvoiceId, ack.Message);
                        }
                    }
                }

                Directory.CreateDirectory(processed);
                var target = Path.Combine(processed, name);
                if (File.Exists(target)) target = Path.Combine(processed, DateTime.Now.ToString("yyyyMMddHHmmss") + "_" + name);
                File.Move(path, target);
                HubLog.Info(HubLog.Events.AckProcessed, "{0}: {1} posted, {2} rejected.", name, accepted, rejected);
            }
        }
    }
}
