using System;
using System.Web.Http;
using Tailspin.DealerOrders.Data.Stores;
using Tailspin.DealerOrders.Processing.Ports;
using Tailspin.DealerOrders.SalesDesk.Infrastructure;

namespace Tailspin.DealerOrders.SalesDesk.Controllers.Api
{
    /// <summary>Queue and sync health for the operations monitoring (polled every 5 minutes).</summary>
    [RoutePrefix("api/status")]
    public class StatusController : ApiController
    {
        private readonly SqlAxCommandQueue _queue;
        private readonly SqlCacheStore _cache;
        private readonly DeskQueries _queries;

        public StatusController(SqlAxCommandQueue queue, SqlCacheStore cache, DeskQueries queries)
        {
            _queue = queue;
            _cache = cache;
            _queries = queries;
        }

        [HttpGet, Route("")]
        public IHttpActionResult Get()
        {
            var counts = _queue.Counts();
            var lastPrices = _cache.LastRun("prices");
            var lastStock = _cache.LastRun("stock");
            var now = DateTime.UtcNow;
            var healthy = (counts.Failed ?? 0) == 0
                          && (!counts.OldestOpenUtc.HasValue || now - counts.OldestOpenUtc.Value < TimeSpan.FromMinutes(30))
                          && lastStock.HasValue && now - lastStock.Value < TimeSpan.FromHours(1);
            return Ok(new
            {
                healthy,
                axQueue = counts,
                pendingApprovals = _queries.PendingApprovals().Count,
                ediProblems24h = _queries.EdiProblems(now.AddDays(-1)),
                lastPriceSyncUtc = lastPrices,
                lastStockSyncUtc = lastStock
            });
        }
    }

    [RoutePrefix("api/orders")]
    public class OrdersApiController : ApiController
    {
        private readonly IOrderStore _orders;

        public OrdersApiController(IOrderStore orders)
        {
            _orders = orders;
        }

        /// <summary>Order by portal number or AX sales id, used by the AX client add-in on the desk PCs.</summary>
        [HttpGet, Route("{number}")]
        public IHttpActionResult Get(string number)
        {
            var order = _orders.FindByNumber(number) ?? _orders.FindByAxSalesId(number);
            if (order == null) return NotFound();
            return Ok(new
            {
                order.Id, order.OrderNumber, order.AxSalesId, order.DealerId, order.DealerPoNumber, order.Status, order.Source,
                order.Currency, order.TotalNet, order.RequestedDeliveryDate, order.ConfirmedDeliveryDate, order.ErrorMessage,
                url = new Uri(Request.RequestUri, "/orders/details/" + order.Id)
            });
        }
    }
}
