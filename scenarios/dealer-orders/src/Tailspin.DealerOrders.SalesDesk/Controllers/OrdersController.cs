using System;
using System.Linq;
using System.Web.Mvc;
using Tailspin.DealerOrders.Data.Queries;
using Tailspin.DealerOrders.Data.Stores;
using Tailspin.DealerOrders.Domain;
using Tailspin.DealerOrders.Processing.Ports;
using Tailspin.DealerOrders.Processing.Sales;
using Tailspin.DealerOrders.SalesDesk.Models;

namespace Tailspin.DealerOrders.SalesDesk.Controllers
{
    public class OrdersController : DeskController
    {
        private readonly OrderQueries _queries;
        private readonly IOrderStore _orders;
        private readonly IDealerStore _dealers;
        private readonly IApprovalStore _approvals;
        private readonly IEdiStore _edi;
        private readonly SqlAxCommandQueue _queue;
        private readonly OrderService _orderService;

        public OrdersController(OrderQueries queries, IOrderStore orders, IDealerStore dealers, IApprovalStore approvals, IEdiStore edi,
            SqlAxCommandQueue queue, OrderService orderService)
        {
            _queries = queries;
            _orders = orders;
            _dealers = dealers;
            _approvals = approvals;
            _edi = edi;
            _queue = queue;
            _orderService = orderService;
        }

        public ActionResult Index(string text, OrderStatus? status, OrderSource? source, int? dealerId, bool openOnly = false, int page = 1)
        {
            ViewBag.Text = text;
            ViewBag.Status = status;
            ViewBag.Source = source;
            ViewBag.OpenOnly = openOnly;
            return View(_queries.Search(new OrderFilter
            {
                Text = text, Status = status, Source = source, DealerId = dealerId, OpenOnly = openOnly, Page = Math.Max(1, page), PageSize = 50
            }));
        }

        public ActionResult Details(int id)
        {
            var order = _orders.Get(id);
            if (order == null) return HttpNotFound();
            return View(new OrderDetails
            {
                Order = order,
                Dealer = _dealers.Get(order.DealerId),
                Commands = _queue.ForOrder(id),
                Approvals = _approvals.ForOrder(id),
                EdiMessages = _edi.ForOrder(id)
            });
        }

        public ActionResult Find(string number)
        {
            var order = _orders.FindByNumber(number) ?? _orders.FindByAxSalesId(number);
            return order == null ? (ActionResult)RedirectToAction("Index", new { text = number }) : RedirectToAction("Details", new { id = order.Id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Resubmit(int id)
        {
            _orderService.Resubmit(id, DeskUser, AxUser);
            Flash("The order was sent to AX again.");
            return RedirectToAction("Details", new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Cancel(int id, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason)) throw new SalesRuleException("Give the reason; the dealer sees it.");
            _orderService.Cancel(id, DeskUser, reason.Trim());
            Flash("The order was cancelled.");
            return RedirectToAction("Details", new { id });
        }
    }
}
