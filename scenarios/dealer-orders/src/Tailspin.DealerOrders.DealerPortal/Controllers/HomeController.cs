using System.Linq;
using System.Web.Mvc;
using AutoMapper;
using Tailspin.DealerOrders.Data.Queries;
using Tailspin.DealerOrders.DealerPortal.Infrastructure;
using Tailspin.DealerOrders.DealerPortal.Models;
using Tailspin.DealerOrders.Domain;
using Tailspin.DealerOrders.Processing.Ports;

namespace Tailspin.DealerOrders.DealerPortal.Controllers
{
    public class HomeController : PortalController
    {
        private readonly IDealerStore _dealers;
        private readonly IOrderStore _orders;
        private readonly IQuoteStore _quotes;
        private readonly OrderQueries _queries;
        private readonly IClock _clock;

        public HomeController(IDealerStore dealers, IOrderStore orders, IQuoteStore quotes, OrderQueries queries, IClock clock)
        {
            _dealers = dealers;
            _orders = orders;
            _quotes = quotes;
            _queries = queries;
            _clock = clock;
        }

        public ActionResult Index()
        {
            var dealer = _dealers.Get(DealerId);
            var recent = _queries.Search(new OrderFilter { DealerId = DealerId, PageSize = 10 });
            var model = new DashboardViewModel
            {
                Dealer = dealer,
                AvailableCredit = dealer.AvailableCredit(_orders.OpenOrderValue(dealer.Id, null)),
                Figures = _queries.Dashboard(dealer.Id, _clock.Today),
                RecentOrders = Mapper.Map<OrderRowViewModel[]>(recent.Items),
                OpenQuotes = Mapper.Map<QuoteViewModel[]>(_quotes.ForDealer(dealer.Id, 50)
                    .Where(q => q.Status != QuoteStatus.Converted && q.Status != QuoteStatus.Cancelled && q.Status != QuoteStatus.Expired)
                    .Take(10))
            };
            return View(model);
        }

        [AllowAnonymous]
        public ActionResult Ping() => Content("OK " + typeof(HomeController).Assembly.GetName().Version, "text/plain");
    }
}
