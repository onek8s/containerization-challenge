using System;
using System.Diagnostics;
using System.Messaging;
using System.Threading;
using Wingtip.Invoices.Infrastructure.Configuration;
using Wingtip.Invoices.Infrastructure.Data;
using Wingtip.Invoices.Infrastructure.Diagnostics;
using Wingtip.Invoices.Infrastructure.Messaging;

namespace Wingtip.Invoices.Processor
{
    /// <summary>
    /// Receives inbound files in MSMQ transactions. A failed message goes back to the queue and is
    /// moved to the poison queue after MaxAttempts.
    /// </summary>
    public class QueueWorker
    {
        private readonly InvoicePipeline pipeline;
        private readonly HubCounters counters;
        private readonly WaitHandle stopping;
        private readonly FileRepository files = new FileRepository();
        private readonly int maxAttempts = HubSettings.GetInt("MaxAttempts", 5);

        public QueueWorker(InvoicePipeline pipeline, HubCounters counters, WaitHandle stopping)
        {
            this.pipeline = pipeline;
            this.counters = counters;
            this.stopping = stopping;
        }

        public void Run()
        {
            using (var inbound = HubQueues.OpenInbound())
            using (var posting = HubQueues.OpenPosting())
            {
                while (!stopping.WaitOne(0))
                {
                    try
                    {
                        ReceiveOne(inbound, posting);
                    }
                    catch (Exception ex)
                    {
                        HubLog.Error(HubLog.Events.Unhandled, ex, "Receiving from {0} failed.", inbound.Path);
                        stopping.WaitOne(TimeSpan.FromSeconds(30));
                    }
                }
            }
        }

        private void ReceiveOne(MessageQueue inbound, MessageQueue posting)
        {
            using (var tx = new MessageQueueTransaction())
            {
                tx.Begin();
                Message message;
                try
                {
                    message = inbound.Receive(TimeSpan.FromSeconds(5), tx);
                }
                catch (MessageQueueException ex) when (ex.MessageQueueErrorCode == MessageQueueErrorCode.IOTimeout)
                {
                    tx.Abort();
                    return;
                }

                InboundFileMessage body;
                try
                {
                    body = (InboundFileMessage)message.Body;
                }
                catch (InvalidOperationException ex)
                {
                    HubLog.Error(HubLog.Events.MovedToPoison, ex, "Message {0} ({1}) cannot be read and was moved to the poison queue.", message.Id, message.Label);
                    MoveToPoison(message, tx);
                    tx.Commit();
                    return;
                }

                var watch = Stopwatch.StartNew();
                try
                {
                    pipeline.Process(body, posting, tx);
                    tx.Commit();
                    counters.ProcessingTime(watch.ElapsedTicks);
                }
                catch (ErpUnavailableException ex)
                {
                    tx.Abort();
                    HubLog.Warning(HubLog.Events.ErpUnavailable, "File {0} waits for the ERP lookup: {1}", body.FileId, ex.Message);
                    stopping.WaitOne(TimeSpan.FromSeconds(60));
                }
                catch (Exception ex)
                {
                    tx.Abort();
                    counters.Failure();
                    var attempts = files.AddAttempt(body.FileId, ex.Message);
                    if (attempts >= maxAttempts)
                    {
                        HubLog.Error(HubLog.Events.MovedToPoison, ex, "File {0} {1} failed {2} times and was moved to the poison queue.", body.FileId, body.FileName, attempts);
                        MoveToPoison(inbound, message.Id, body.FileId);
                    }
                    else
                    {
                        HubLog.Error(HubLog.Events.ProcessingFailed, ex, "File {0} {1} failed (attempt {2} of {3}).", body.FileId, body.FileName, attempts, maxAttempts);
                        stopping.WaitOne(TimeSpan.FromSeconds(10 * attempts));
                    }
                }
            }
        }

        private void MoveToPoison(MessageQueue inbound, string messageId, long fileId)
        {
            using (var tx = new MessageQueueTransaction())
            {
                tx.Begin();
                var message = inbound.ReceiveById(messageId, tx);
                MoveToPoison(message, tx);
                tx.Commit();
            }
            files.SetStatus(fileId, FileStatus.Poison, null);
        }

        private static void MoveToPoison(Message message, MessageQueueTransaction tx)
        {
            using (var poison = new MessageQueue(HubQueues.PoisonPath))
            {
                message.Formatter = new XmlMessageFormatter(new[] { typeof(InboundFileMessage) });
                poison.Send(message, tx);
            }
        }
    }
}
