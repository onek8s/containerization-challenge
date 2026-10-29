using System;
using System.Messaging;
using System.Threading;
using Wingtip.Invoices.Core.Erp;
using Wingtip.Invoices.Infrastructure.Data;
using Wingtip.Invoices.Infrastructure.Diagnostics;
using Wingtip.Invoices.Infrastructure.Messaging;

namespace Wingtip.Invoices.ErpLink
{
    /// <summary>Moves released invoices from the posting queue into the posting table.</summary>
    public class PostingReceiver
    {
        private readonly PostingRepository postings;
        private readonly WaitHandle stopping;

        public PostingReceiver(PostingRepository postings, WaitHandle stopping)
        {
            this.postings = postings;
            this.stopping = stopping;
        }

        public void Run()
        {
            using (var queue = HubQueues.OpenPosting())
            {
                while (!stopping.WaitOne(0))
                {
                    using (var tx = new MessageQueueTransaction())
                    {
                        tx.Begin();
                        try
                        {
                            var message = queue.Receive(TimeSpan.FromSeconds(5), tx);
                            var invoice = (PostingInvoice)message.Body;
                            postings.Enqueue(invoice);
                            tx.Commit();
                            HubLog.Verbose("Invoice {0} queued for posting.", invoice.InvoiceId);
                        }
                        catch (MessageQueueException ex) when (ex.MessageQueueErrorCode == MessageQueueErrorCode.IOTimeout)
                        {
                            tx.Abort();
                        }
                        catch (Exception ex)
                        {
                            tx.Abort();
                            HubLog.Error(HubLog.Events.PostingFailed, ex, "Receiving from the posting queue failed.");
                            stopping.WaitOne(TimeSpan.FromSeconds(30));
                        }
                    }
                }
            }
        }
    }
}
