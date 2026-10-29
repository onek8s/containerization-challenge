using System.Linq;
using System.Web.Mvc;
using Tailspin.DealerOrders.Configurator;
using Tailspin.DealerOrders.Data;
using Tailspin.DealerOrders.Data.Queries;
using Tailspin.DealerOrders.DealerPortal.Infrastructure;
using Tailspin.DealerOrders.DealerPortal.Models;
using Tailspin.DealerOrders.Domain;
using Tailspin.DealerOrders.Processing.Ports;
using Tailspin.DealerOrders.Processing.Sales;

namespace Tailspin.DealerOrders.DealerPortal.Controllers
{
    /// <summary>Quick order for spare parts: item lookup with price and stock, then one order for all lines.</summary>
    [PortalRole(PortalRoles.DealerParts, PortalRoles.DealerAdmin)]
    public class PartsController : PortalController
    {
        private readonly ICatalog _catalog;
        private readonly IDealerStore _dealers;
        private readonly PriceCalculator _prices;
        private readonly OrderQueries _queries;
        private readonly OrderService _orders;
        private readonly IClock _clock;

        public PartsController(ICatalog catalog, IDealerStore dealers, PriceCalculator prices, OrderQueries queries, OrderService orders, IClock clock)
        {
            _catalog = catalog;
            _dealers = dealers;
            _prices = prices;
            _queries = queries;
            _orders = orders;
            _clock = clock;
        }

        public ActionResult Index()
        {
            ViewBag.Addresses = _dealers.Get(DealerId).Addresses.ToList();
            return View();
        }

        [HttpGet]
        public ActionResult Lookup(string itemId, decimal quantity = 1)
        {
            itemId = (itemId ?? "").Trim().ToUpperInvariant();
            var dealer = _dealers.Get(DealerId);
            var description = _catalog.PartDescription(itemId);
            if (description == null) return JsonNet(new { found = false, itemId });
            var price = _prices.PricePart(itemId, description, dealer, quantity <= 0 ? 1 : quantity, _clock.Today);
            var stock = _queries.Stock(itemId, dealer.Warehouse, 5).Where(s => s.ItemId == itemId).ToList();
            return JsonNet(new
            {
                found = true,
                itemId,
                description,
                currency = dealer.Currency,
                listPrice = price.ListPrice,
                discountPercent = price.DiscountPercent,
                netPrice = price.NetPrice,
                note = price.Note,
                available = stock.Sum(s => s.Available),
                nextReceipt = stock.Where(s => s.NextReceiptDate.HasValue).Select(s => s.NextReceiptDate).Min()
            });
        }

        [HttpPost, ValidateJsonAntiForgeryToken]
        public ActionResult Place()
        {
            var input = FromJsonBody<PartsOrderModel>();
            var request = new NewOrderRequest
            {
                DealerId = DealerId,
                User = UserName,
                Source = OrderSource.Portal,
                DealerPoNumber = input.DealerPoNumber,
                RequestedDeliveryDate = input.RequestedDelivery,
                DeliveryAddressId = input.DeliveryAddressId,
                Notes = input.Notes
            };
            foreach (var line in input.Lines.Where(l => !string.IsNullOrWhiteSpace(l.ItemId) && l.Quantity > 0))
                request.Parts.Add(new PartLineRequest { ItemId = line.ItemId.Trim().ToUpperInvariant(), Quantity = line.Quantity, DealerLineReference = line.Reference });
            if (request.Parts.Count == 0) throw new SalesRuleException("Add at least one part.");
            var order = _orders.CreateOrder(request);
            return JsonNet(new { orderId = order.Id, orderNumber = order.OrderNumber, url = Url.Action("Details", "Orders", new { id = order.Id }) });
        }
    }
}
