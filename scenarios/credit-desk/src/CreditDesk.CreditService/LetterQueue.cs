using System;
using System.Messaging;
using System.Security.Principal;
using CreditDesk.Core;

namespace CreditDesk.CreditService
{
    /// <summary>Sends decisions to the transactional letter queue on the document server.</summary>
    internal sealed class LetterQueue
    {
        private readonly string _formatName;

        public LetterQueue(string formatName)
        {
            _formatName = formatName;
        }

        /// <summary>
        /// Must be called inside the TransactionScope that records the decision, so the message is
        /// only delivered when the database update commits (coordinated by MSDTC).
        /// </summary>
        public void Send(LetterMessage letter)
        {
            // The queue ACL grants send rights to the service account, not to individual users.
            using (WindowsIdentity.Impersonate(IntPtr.Zero))
            using (var queue = new MessageQueue(_formatName, QueueAccessMode.Send))
            {
                queue.Formatter = new XmlMessageFormatter(new[] { typeof(LetterMessage) });
                using (var message = new Message(letter)
                {
                    Label = LetterFields.Reference(letter),
                    Recoverable = true,
                    UseDeadLetterQueue = true,
                    TimeToReachQueue = TimeSpan.FromDays(3)
                })
                {
                    queue.Send(message, MessageQueueTransactionType.Automatic);
                }
            }
        }
    }
}
