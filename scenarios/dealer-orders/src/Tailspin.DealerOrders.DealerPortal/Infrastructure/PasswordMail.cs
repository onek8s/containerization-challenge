using System.Collections.Generic;
using log4net;
using Tailspin.DealerOrders.Data;
using Tailspin.DealerOrders.Domain;
using Tailspin.DealerOrders.Processing.Notifications;
using Tailspin.DealerOrders.Processing.Ports;

namespace Tailspin.DealerOrders.DealerPortal.Infrastructure
{
    /// <summary>Queues the password reset (or new user invitation) mail; the notification service sends it.</summary>
    public class PasswordMail
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(PasswordMail));

        private readonly INotificationStore _notifications;
        private readonly IClock _clock;

        public PasswordMail(INotificationStore notifications, IClock clock)
        {
            _notifications = notifications;
            _clock = clock;
        }

        public void Send(PortalUser user, string link)
        {
            var text = NotificationComposer.Render(NotificationKind.PasswordReset, user.LanguageCode ?? "en",
                new Dictionary<string, string> { ["UserName"] = user.DisplayName ?? user.UserName, ["Link"] = link });
            _notifications.Add(new Notification
            {
                Kind = NotificationKind.PasswordReset,
                DealerId = user.DealerId,
                Recipient = user.Email,
                Subject = text.Item1,
                Body = text.Item2,
                CreatedUtc = _clock.UtcNow
            });
            Log.InfoFormat("Password mail queued for {0}", user.UserName);
        }
    }
}
