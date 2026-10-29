using System.Linq;
using Tailspin.DealerOrders.Domain;
using Tailspin.DealerOrders.Processing.Notifications;
using Tailspin.DealerOrders.Processing.Ports;

namespace Tailspin.DealerOrders.Processing.Sync
{
    /// <summary>Daily open order digest for dealers that have open orders or expiring quotes.</summary>
    public class DigestBuilder
    {
        private readonly IDealerStore _dealers;
        private readonly IOrderStore _orders;
        private readonly IQuoteStore _quotes;
        private readonly NotificationComposer _notifications;
        private readonly IClock _clock;

        public DigestBuilder(IDealerStore dealers, IOrderStore orders, IQuoteStore quotes, NotificationComposer notifications, IClock clock)
        {
            _dealers = dealers;
            _orders = orders;
            _quotes = quotes;
            _notifications = notifications;
            _clock = clock;
        }

        public int Run()
        {
            var sent = 0;
            var openQuotes = _quotes.Open();
            foreach (var dealer in _dealers.All().Where(d => d.Status == DealerStatus.Active && !string.IsNullOrEmpty(d.Email)))
            {
                var open = _orders.ForDealer(dealer.Id, 500).Where(o => o.IsOpen && o.Status != OrderStatus.Draft).ToList();
                var expiring = openQuotes.Where(q => q.DealerId == dealer.Id && q.ValidUntil <= _clock.Today.AddDays(7) && q.ValidUntil >= _clock.Today).ToList();
                if (open.Count == 0 && expiring.Count == 0) continue;
                _notifications.DailyDigest(dealer, open, expiring);
                sent++;
            }
            return sent;
        }
    }
}
