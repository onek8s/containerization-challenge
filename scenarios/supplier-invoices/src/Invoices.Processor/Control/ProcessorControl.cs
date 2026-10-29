using System;
using System.Collections.Generic;
using System.Linq;
using System.Messaging;
using System.ServiceModel;
using Wingtip.Invoices.Contracts.Control;
using Wingtip.Invoices.Infrastructure.Data;
using Wingtip.Invoices.Infrastructure.Diagnostics;
using Wingtip.Invoices.Infrastructure.Messaging;

namespace Wingtip.Invoices.Processor.Control
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerCall, ConcurrencyMode = ConcurrencyMode.Multiple,
        Namespace = "http://schemas.wingtip.fi/invoices/2019/control")]
    public class ProcessorControl : IProcessorControl
    {
        internal static InvoicePipeline Pipeline { get; set; }

        public QueueStatus[] GetQueueStatus()
        {
            return new[]
            {
                Status("Inbound", HubQueues.InboundPath),
                Status("Poison", HubQueues.PoisonPath),
                Status("Posting", HubQueues.PostingPath)
            };
        }

        public void ResubmitFile(long fileId, string requestedBy)
        {
            Guard(() =>
            {
                var files = new FileRepository();
                var file = files.Get(fileId);
                if (file == null) throw new InvalidOperationException("File " + fileId + " does not exist.");
                if (string.IsNullOrEmpty(file.ArchivePath)) throw new InvalidOperationException("File " + fileId + " has no archived original.");

                var supplier = new InvoiceRepository().GetSupplier(file.SupplierId);
                var message = new InboundFileMessage
                {
                    FileId = file.FileId,
                    SupplierId = file.SupplierId,
                    SupplierCode = supplier.Code,
                    FileName = file.FileName,
                    ArchivePath = file.ArchivePath,
                    ReceivedAt = DateTime.Now,
                    ResubmittedBy = requestedBy
                };
                files.SetStatus(fileId, FileStatus.Queued, null);
                HubQueues.Send(HubQueues.InboundPath, typeof(InboundFileMessage), message, supplier.Code + " " + file.FileName + " (resubmitted)");
                Audit("ResubmitFile", requestedBy, fileId);
            });
        }

        public MatchSummary RematchInvoice(long invoiceId, string requestedBy)
        {
            MatchSummary summary = null;
            Guard(() =>
            {
                var result = Pipeline.Rematch(invoiceId, requestedBy);
                summary = new MatchSummary
                {
                    InvoiceId = invoiceId,
                    Matched = result.Matched,
                    Summary = result.Summary,
                    Issues = result.Issues.Select(i => i.ToString()).ToArray()
                };
                Audit("RematchInvoice", requestedBy, invoiceId);
            });
            return summary;
        }

        public void ReleaseInvoice(long invoiceId, string releasedBy, string comment)
        {
            Guard(() =>
            {
                using (var posting = HubQueues.OpenPosting())
                {
                    Pipeline.Release(invoiceId, releasedBy, comment, posting);
                }
                Audit("ReleaseInvoice", releasedBy, invoiceId);
            });
        }

        private static QueueStatus Status(string name, string path)
        {
            var status = new QueueStatus { Name = name, Path = path };
            try
            {
                using (var queue = new MessageQueue(path))
                {
                    queue.MessageReadPropertyFilter.ClearAll();
                    queue.MessageReadPropertyFilter.ArrivedTime = true;
                    var count = 0;
                    DateTime? oldest = null;
                    using (var e = queue.GetMessageEnumerator2())
                    {
                        while (e.MoveNext(TimeSpan.Zero))
                        {
                            count++;
                            if (oldest == null) oldest = e.Current.ArrivedTime;
                        }
                    }
                    status.Messages = count;
                    status.OldestMessage = oldest;
                }
            }
            catch (MessageQueueException)
            {
                // Remote queue that cannot be read from here.
                status.Messages = -1;
            }
            return status;
        }

        private static void Guard(Action action)
        {
            try
            {
                action();
            }
            catch (InvalidOperationException ex)
            {
                throw new FaultException<ControlFault>(new ControlFault { Code = "InvalidState", Message = ex.Message }, ex.Message);
            }
            catch (ErpUnavailableException ex)
            {
                throw new FaultException<ControlFault>(new ControlFault { Code = "ErpUnavailable", Message = ex.Message }, ex.Message);
            }
        }

        private static void Audit(string operation, string user, long id)
        {
            var caller = ServiceSecurityContext.Current == null ? "?" : ServiceSecurityContext.Current.PrimaryIdentity.Name;
            HubLog.Info(HubLog.Events.ControlCall, "{0}({1}) for {2} called by {3}.", operation, id, user, caller);
        }
    }
}
