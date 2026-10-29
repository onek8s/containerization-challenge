using System;
using System.Web.Mvc;
using Tailspin.DealerOrders.Data.Queries;
using Tailspin.DealerOrders.Data.Stores;
using Tailspin.DealerOrders.Domain;
using Tailspin.DealerOrders.SalesDesk.Infrastructure;
using Tailspin.DealerOrders.SalesDesk.Models;

namespace Tailspin.DealerOrders.SalesDesk.Controllers
{
    public class HomeController : DeskController
    {
        private readonly DeskQueries _desk;
        private readonly OrderQueries _orders;
        private readonly SqlAxCommandQueue _queue;
        private readonly SqlCacheStore _cache;

        public HomeController(DeskQueries desk, OrderQueries orders, SqlAxCommandQueue queue, SqlCacheStore cache)
        {
            _desk = desk;
            _orders = orders;
            _queue = queue;
            _cache = cache;
        }

        public ActionResult Index()
        {
            return View(new DeskDashboard
            {
                Figures = _orders.Dashboard(null, DateTime.Today),
                Approvals = _desk.PendingApprovals(),
                OrdersInError = _orders.Search(new OrderFilter { Status = OrderStatus.Error, PageSize = 20 }).Items,
                Queue = _queue.Counts(),
                EdiProblems = _desk.EdiProblems(DateTime.UtcNow.AddDays(-2)),
                LastPriceSync = _cache.LastRun("prices"),
                LastStockSync = _cache.LastRun("stock")
            });
        }
    }
}
