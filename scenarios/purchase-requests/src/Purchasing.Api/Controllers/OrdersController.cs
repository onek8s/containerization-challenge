using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Web.Http;
using Contoso.Purchasing.Api.Infrastructure;
using Contoso.Purchasing.Api.Models;
using Contoso.Purchasing.Core;
using Contoso.Purchasing.Data;

namespace Contoso.Purchasing.Api.Controllers
{
    [RoutePrefix("api/orders")]
    public class OrdersController : PurchasingController
    {
        [HttpGet, Route("")]
        public IList<PurchaseOrder> List(int days = 60)
        {
            EnsureBuyerOrFinance();
            return new OrderRepository(Db).Recent(days);
        }

        [HttpGet, Route("{id:int}")]
        public PurchaseOrder Get(int id)
        {
            var order = new OrderRepository(Db).Get(id);
            if (order == null) throw new HttpResponseException(HttpStatusCode.NotFound);
            if (!CanView(order.Request)) throw new HttpResponseException(HttpStatusCode.Forbidden);
            return order;
        }

        [HttpPost, Route("")]
        public PurchaseOrder Create(OrderInput input)
        {
            if (!CurrentUser.IsBuyer) throw new HttpResponseException(HttpStatusCode.Forbidden);
            if (input == null || string.IsNullOrWhiteSpace(input.DeliveryAddress))
            {
                throw new PolicyException("Delivery address is required.");
            }

            var orders = new OrderRepository(Db);
            var order = orders.CreateFromRequest(input.RequestId, CurrentUser.Account, input.DeliveryAddress.Trim(), Now);
            Trace.TraceInformation("{0} created purchase order {1} for request {2}", CurrentUser.Account, order.PoNumber, input.RequestId);
            return orders.Get(order.Id);
        }

        private void EnsureBuyerOrFinance()
        {
            if (!(CurrentUser.IsBuyer || CurrentUser.IsFinance || CurrentUser.IsAdministrator))
            {
                throw new HttpResponseException(HttpStatusCode.Forbidden);
            }
        }
    }
}
