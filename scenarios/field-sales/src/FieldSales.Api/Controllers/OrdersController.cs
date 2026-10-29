using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Wingtip.FieldSales.Api.Infrastructure;
using Wingtip.FieldSales.Api.Services;
using Wingtip.FieldSales.Contracts;
using Wingtip.FieldSales.Core;

namespace Wingtip.FieldSales.Api.Controllers
{
    [RoutePrefix("api/orders"), ClientVersion]
    public class OrdersController : ApiController
    {
        private readonly OrderService _orders;

        public OrdersController(OrderService orders)
        {
            _orders = orders;
        }

        [HttpPost, Route("")]
        public async Task<HttpResponseMessage> Submit(OrderSubmitRequest request)
        {
            if (request == null || request.ClientOrderId == Guid.Empty || string.IsNullOrEmpty(request.CustomerNo))
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "ClientOrderId and CustomerNo are required.");
            if (!string.IsNullOrEmpty(request.RepReference) && !RepReference.IsValid(request.RepReference))
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "RepReference has an unknown format.");

            var version = Request.Headers.Contains(ClientVersionAttribute.HeaderName)
                ? Request.Headers.GetValues(ClientVersionAttribute.HeaderName).FirstOrDefault()
                : null;

            var response = await _orders.SubmitAsync(request, User, version);
            switch (response.Result)
            {
                case SubmitResults.Accepted: return Request.CreateResponse(HttpStatusCode.Created, response);
                case SubmitResults.AlreadyReceived: return Request.CreateResponse(HttpStatusCode.OK, response);
                case SubmitResults.PriceChanged: return Request.CreateResponse(HttpStatusCode.Conflict, response);
                default: return Request.CreateResponse((HttpStatusCode)422, response);
            }
        }

        /// <summary>Status of the caller's orders received in the last <paramref name="days"/> days.</summary>
        [HttpGet, Route("")]
        public Task<List<OrderSummaryDto>> Recent(int days = 30)
        {
            return _orders.RecentAsync(User, DateTime.Today.AddDays(-Math.Min(Math.Max(days, 1), 180)));
        }
    }
}
