using System;
using System.Messaging;
using Wingtip.Invoices.Core.Erp;
using Wingtip.Invoices.Infrastructure.Configuration;

namespace Wingtip.Invoices.Infrastructure.Messaging
{
    /// <summary>
    /// Message queues of the hub. Paths come from appSettings so that a queue on another server
    /// can be addressed with a direct format name, e.g.
    /// FormatName:DIRECT=OS:invoices-proc.corp.local\private$\invoices-inbound
    /// </summary>
    public static class HubQueues
    {
        /// <summary>Largest file sent inside the message. MSMQ messages are limited to 4 MB.</summary>
        public const int InlineContentLimit = 2 * 1024 * 1024;

        public static string InboundPath
        {
            get { return HubSettings.Get("InboundQueue", @".\private$\invoices-inbound"); }
        }

        public static string PoisonPath
        {
            get { return HubSettings.Get("PoisonQueue", @".\private$\invoices-poison"); }
        }

        public static string PostingPath
        {
            get { return HubSettings.Get("PostingQueue", @".\private$\invoices-posting"); }
        }

        public static MessageQueue Open(string path, Type bodyType)
        {
            var queue = new MessageQueue(path)
            {
                Formatter = new XmlMessageFormatter(new[] { bodyType })
            };
            queue.MessageReadPropertyFilter.SetAll();
            return queue;
        }

        public static MessageQueue OpenInbound()
        {
            return Open(InboundPath, typeof(InboundFileMessage));
        }

        public static MessageQueue OpenPosting()
        {
            return Open(PostingPath, typeof(PostingInvoice));
        }

        public static Message CreateMessage(object body, string label)
        {
            return new Message(body)
            {
                Label = label.Length > 124 ? label.Substring(0, 124) : label,
                Recoverable = true,
                UseDeadLetterQueue = true,
                TimeToReachQueue = TimeSpan.FromDays(3)
            };
        }

        /// <summary>Sends inside an MSMQ transaction (remote queues are reached through the local outgoing queue).</summary>
        public static void Send(MessageQueue queue, object body, string label, MessageQueueTransaction transaction)
        {
            using (var message = CreateMessage(body, label))
            {
                queue.Send(message, transaction);
            }
        }

        public static void Send(string path, Type bodyType, object body, string label)
        {
            using (var queue = Open(path, bodyType))
            using (var tx = new MessageQueueTransaction())
            {
                tx.Begin();
                Send(queue, body, label, tx);
                tx.Commit();
            }
        }
    }
}
