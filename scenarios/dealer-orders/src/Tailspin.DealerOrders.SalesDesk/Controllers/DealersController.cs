using System.Linq;
using System.Web.Mvc;
using Tailspin.DealerOrders.Data.Queries;
using Tailspin.DealerOrders.Processing.Ports;
using Tailspin.DealerOrders.Processing.Sales;
using Tailspin.DealerOrders.SalesDesk.Infrastructure;
using Tailspin.DealerOrders.SalesDesk.Models;

namespace Tailspin.DealerOrders.SalesDesk.Controllers
{
    public class DealersController : DeskController
    {
        private readonly IDealerStore _dealers;
        private readonly IOrderStore _orders;
        private readonly IEdiStore _edi;
        private readonly OrderQueries _queries;
        private readonly AxCommandFactory _commands;

        public DealersController(IDealerStore dealers, IOrderStore orders, IEdiStore edi, OrderQueries queries, AxCommandFactory commands)
        {
            _dealers = dealers;
            _orders = orders;
            _edi = edi;
            _queries = queries;
            _commands = commands;
        }

        public ActionResult Index(string search)
        {
            ViewBag.Search = search;
            var dealers = _dealers.All().AsEnumerable();
            if (!string.IsNullOrWhiteSpace(search))
                dealers = dealers.Where(d => d.Name.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) >= 0 || d.AccountNumber.Contains(search.ToUpperInvariant()));
            return View(dealers.OrderBy(d => d.Name).ToList());
        }

        public ActionResult Details(int id)
        {
            var dealer = _dealers.Get(id);
            if (dealer == null) return HttpNotFound();
            return View(new DealerDetails
            {
                Dealer = dealer,
                OpenOrderValue = _orders.OpenOrderValue(id, null),
                Orders = _queries.Search(new OrderFilter { DealerId = id, PageSize = 20 }).Items,
                EdiPartner = _edi.PartnerForDealer(id)
            });
        }

        /// <summary>Sends the dealer's master data and addresses to AX (customer, delivery addresses, price group).</summary>
        [HttpPost, ValidateAntiForgeryToken, DeskAuthorize(DeskRole.DealerAdmin, DeskRole.SalesManager)]
        public ActionResult Sync(int id)
        {
            var dealer = _dealers.Get(id);
            if (dealer == null) return HttpNotFound();
            _commands.ForDealer(dealer, DeskUser);
            Flash($"{dealer.AccountNumber} queued for AX.");
            return RedirectToAction("Details", new { id });
        }
    }
}
