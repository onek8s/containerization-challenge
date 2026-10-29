using System.Linq;
using System.Web.Mvc;
using AutoMapper;
using Tailspin.DealerOrders.Data;
using Tailspin.DealerOrders.DealerPortal.Infrastructure;
using Tailspin.DealerOrders.DealerPortal.Models;
using Tailspin.DealerOrders.Domain;
using Tailspin.DealerOrders.Infrastructure.DocumentService;
using Tailspin.DealerOrders.Processing.Ports;
using Tailspin.DealerOrders.Processing.Sales;

namespace Tailspin.DealerOrders.DealerPortal.Controllers
{
    [PortalRole(PortalRoles.DealerSales, PortalRoles.DealerAdmin)]
    public class QuotesController : PortalController
    {
        private readonly IQuoteStore _quotes;
        private readonly IDealerStore _dealers;
        private readonly QuoteService _quoteService;
        private readonly OrderService _orderService;
        private readonly DocumentServiceClient _documents;
        private readonly IClock _clock;

        public QuotesController(IQuoteStore quotes, IDealerStore dealers, QuoteService quoteService, OrderService orderService, DocumentServiceClient documents, IClock clock)
        {
            _quotes = quotes;
            _dealers = dealers;
            _quoteService = quoteService;
            _orderService = orderService;
            _documents = documents;
            _clock = clock;
        }

        public ActionResult Index(bool all = false)
        {
            var quotes = _quotes.ForDealer(DealerId, all ? 500 : 100).AsEnumerable();
            if (!all) quotes = quotes.Where(q => q.Status != QuoteStatus.Cancelled && q.Status != QuoteStatus.Expired && q.Status != QuoteStatus.Converted);
            ViewBag.All = all;
            return View(Mapper.Map<QuoteViewModel[]>(quotes.ToList()));
        }

        public ActionResult Details(int id)
        {
            var quote = Own(id);
            if (quote == null) return HttpNotFound();
            var model = Mapper.Map<QuoteViewModel>(quote);
            model.CanAccept = quote.CanAccept(_clock.Today);
            model.DeliveryAddresses = _dealers.Get(DealerId).Addresses.ToList();
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult AddPart(int id, string itemId, decimal quantity)
        {
            if (Own(id) == null) return HttpNotFound();
            _quoteService.AddPart(id, (itemId ?? "").Trim().ToUpperInvariant(), quantity);
            return RedirectToAction("Details", new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult RemoveLine(int id, int lineNumber)
        {
            if (Own(id) == null) return HttpNotFound();
            _quoteService.RemoveLine(id, lineNumber);
            return RedirectToAction("Details", new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Submit(int id, decimal extraDiscount = 0)
        {
            if (Own(id) == null) return HttpNotFound();
            _quoteService.Submit(id, UserName, extraDiscount);
            var quote = _quotes.Get(id);
            Flash(quote.Status == QuoteStatus.PendingApproval
                ? $"Quote {quote.QuoteNumber} was sent to the Tailspin sales desk for approval of the extra discount."
                : $"Quote {quote.QuoteNumber} is ready. You can print it or turn it into an order.");
            return RedirectToAction("Details", new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Accept(AcceptQuoteModel model)
        {
            if (Own(model.QuoteId) == null) return HttpNotFound();
            if (!ModelState.IsValid)
            {
                FlashError("Enter your purchase order number.");
                return RedirectToAction("Details", new { id = model.QuoteId });
            }
            var order = _orderService.AcceptQuote(model.QuoteId, UserName, model.DealerPoNumber, model.RequestedDelivery, model.DeliveryAddressId);
            Flash($"Order {order.OrderNumber} was created from the quote.");
            return RedirectToAction("Details", "Orders", new { id = order.Id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Cancel(int id)
        {
            if (Own(id) == null) return HttpNotFound();
            _quoteService.Cancel(id, UserName);
            return RedirectToAction("Index");
        }

        /// <summary>Generates the quote document (Word) through the document service and downloads it.</summary>
        public ActionResult Document(int id)
        {
            var quote = Own(id);
            if (quote == null) return HttpNotFound();
            var info = _documents.Call(s => s.GenerateQuote(id, UserName));
            return RedirectToAction("Download", "Documents", new { id = info.Id });
        }

        private Quote Own(int id)
        {
            var quote = _quotes.Get(id);
            return quote != null && quote.DealerId == DealerId ? quote : null;
        }
    }
}
