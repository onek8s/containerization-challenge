using System.Web.Mvc;
using Tailspin.DealerOrders.Data.Queries;
using Tailspin.DealerOrders.DealerPortal.Infrastructure;
using Tailspin.DealerOrders.Processing.Ports;

namespace Tailspin.DealerOrders.DealerPortal.Controllers
{
    /// <summary>Factory and warehouse stock from the cache that the sync service refreshes from AX.</summary>
    public class StockController : PortalController
    {
        private readonly OrderQueries _queries;
        private readonly ICacheStore _cache;

        public StockController(OrderQueries queries, ICacheStore cache)
        {
            _queries = queries;
            _cache = cache;
        }

        public ActionResult Index(string search, string warehouse)
        {
            ViewBag.Search = search;
            ViewBag.Warehouse = warehouse;
            ViewBag.LastSync = _cache.LastRun("stock");
            return View(string.IsNullOrWhiteSpace(search) && string.IsNullOrWhiteSpace(warehouse)
                ? new StockRow[0]
                : (object)_queries.Stock(search?.Trim(), string.IsNullOrWhiteSpace(warehouse) ? null : warehouse));
        }
    }
}
