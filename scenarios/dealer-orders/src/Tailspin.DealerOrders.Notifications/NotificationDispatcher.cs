using System;
using log4net;
using Tailspin.DealerOrders.Infrastructure.Mail;
using Tailspin.DealerOrders.Processing.Ports;

namespace Tailspin.DealerOrders.Notifications
{
    /// <summary>Sends pending notifications: mail through SMTP, then the push payload to the portal.</summary>
    public class NotificationDispatcher
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(NotificationDispatcher));

        private readonly INotificationStore _store;
        private readonly IMailSender _mail;
        private readonly PortalPushClient _push;
        private readonly IClock _clock;

        public NotificationDispatcher(INotificationStore store, IMailSender mail, PortalPushClient push, IClock clock)
        {
            _store = store;
            _mail = mail;
            _push = push;
            _clock = clock;
        }

        public int Run(int max)
        {
            var pending = _store.ClaimPending(max, _clock.UtcNow);
            if (pending.Count == 0) return 0;
            var canPush = _push.EnsureConnected();
            var done = 0;
            foreach (var n in pending)
            {
                try
                {
                    if (n.MailSentUtc == null)
                    {
                        if (!string.IsNullOrWhiteSpace(n.Recipient)) _mail.Send(n.Recipient, n.Subject, n.Body);
                        n.MailSentUtc = _clock.UtcNow;
                    }

                    if (n.PushPayload != null && n.PushedUtc == null)
                    {
                        if (!_push.Enabled) n.PushedUtc = _clock.UtcNow;
                        else if (canPush && n.DealerId.HasValue)
                        {
                            _push.Publish(n.DealerId.Value, n.PushPayload).Wait(TimeSpan.FromSeconds(15));
                            n.PushedUtc = _clock.UtcNow;
                        }
                        else if (!n.DealerId.HasValue) n.PushedUtc = _clock.UtcNow;
                    }

                    n.LastError = null;
                    done++;
                }
                catch (Exception ex)
                {
                    n.Attempts++;
                    n.LastError = ex.GetBaseException().Message;
                    Log.Warn($"Notification {n.Id} ({n.Kind} to {n.Recipient}) failed, attempt {n.Attempts}", ex);
                }
                _store.Save(n);
            }
            return done;
        }
    }
}
