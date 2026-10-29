using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProductReturns.Api.Security;
using ProductReturns.Api.Services;
using ProductReturns.Core.Api;

namespace ProductReturns.Api.Controllers
{
    [Route("api/orders")]
    [Authorize(Policies.Portal)]
    public class OrdersController : ApiControllerBase
    {
        private readonly ReturnService _returns;

        public OrdersController(ReturnService returns)
        {
            _returns = returns;
        }

        /// <summary>Order with the lines that can still be returned. Customers must give the order e-mail address.</summary>
        [HttpGet("{orderNumber}")]
        public async Task<ActionResult<OrderLookupResponse>> Get(string orderNumber, [FromQuery] string email, CancellationToken ct)
        {
            var order = await _returns.LookupOrderAsync(orderNumber, email, ct);
            if (order == null)
            {
                return NotFound();
            }

            return order;
        }
    }
}
