using System;
using System.Linq;
using System.Web.Mvc;
using AutoMapper;
using Tailspin.DealerOrders.Data.Queries;
using Tailspin.DealerOrders.Data.Stores;
using Tailspin.DealerOrders.DealerPortal.Infrastructure;
using Tailspin.DealerOrders.DealerPortal.Models;
using Tailspin.DealerOrders.Documents.Excel;
using Tailspin.DealerOrders.Domain;
using Tailspin.DealerOrders.Infrastructure.DocumentService;
using Tailspin.DealerOrders.Processing.Ports;
using Tailspin.DealerOrders.Processing.Sales;

namespace Tailspin.DealerOrders.DealerPortal.Controllers
{
    public class OrdersController : PortalController
    {
        private const string ExcelType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        private readonly IOrderStore _orders;
        private readonly IDealerStore _dealers;
        private readonly OrderQueries _queries;
        private readonly OrderService _orderService;
        private readonly SqlDocumentStore _documentStore;
        private readonly DocumentServiceClient _documents;
        private readonly IClock _clock;

        public OrdersController(IOrderStore orders, IDealerStore dealers, OrderQueries queries, OrderService orderService, SqlDocumentStore documentStore,
            DocumentServiceClient documents, IClock clock)
        {
            _orders = orders;
            _dealers = dealers;
            _queries = queries;
            _orderService = orderService;
            _documentStore = documentStore;
            _documents = documents;
            _clock = clock;
        }

        public ActionResult Index(OrderListViewModel model)
        {
            var result = _queries.Search(new OrderFilter
            {
                DealerId = DealerId,
                Text = model.Text,
                Status = model.Status,
                OpenOnly = model.OpenOnly,
                Page = Math.Max(1, model.Page),
                PageSize = 25
            });
            model.Orders = Mapper.Map<OrderRowViewModel[]>(result.Items);
            model.Pages = result.Pages;
            model.Total = result.Total;
            return View(model);
        }

        public ActionResult Details(int id)
        {
            var order = Own(id);
            if (order == null) return HttpNotFound();
            var model = Mapper.Map<OrderViewModel>(order);
            model.Documents = _documentStore.List(DealerId, null, id);
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Cancel(int id, string reason)
        {
            if (Own(id) == null) return HttpNotFound();
            _orderService.Cancel(id, UserName, string.IsNullOrWhiteSpace(reason) ? "Cancelled by dealer" : reason.Trim());
            Flash("The order was cancelled.");
            return RedirectToAction("Details", new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult ChangeDate(int id, DateTime requestedDate)
        {
            if (Own(id) == null) return HttpNotFound();
            _orderService.ChangeRequestedDate(id, UserName, requestedDate);
            Flash("The requested delivery date was sent to Tailspin.");
            return RedirectToAction("Details", new { id });
        }

        public ActionResult Confirmation(int id)
        {
            if (Own(id) == null) return HttpNotFound();
            var info = _documents.Call(s => s.GenerateOrderConfirmation(id, UserName));
            return RedirectToAction("Download", "Documents", new { id = info.Id });
        }

        /// <summary>The dealer's open orders as an Excel sheet.</summary>
        public ActionResult Export()
        {
            var dealer = _dealers.Get(DealerId);
            var orders = _orders.ForDealer(DealerId, 1000).Where(o => o.IsOpen).ToList();
            var content = OrderExportWorkbook.Build(orders, _ => dealer.Name);
            return File(content, ExcelType, $"Open-orders-{dealer.AccountNumber}-{_clock.Today:yyyyMMdd}.xlsx");
        }

        private SalesOrder Own(int id)
        {
            var order = _orders.Get(id);
            return order != null && order.DealerId == DealerId ? order : null;
        }
    }
}
