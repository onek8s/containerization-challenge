using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Litware.OrderHub.Contracts;
using Litware.OrderHub.Core.Channels;
using MassTransit;
using Serilog;

namespace Litware.OrderHub.Intake.Api
{
    /// <summary>Target of the webshop "Order updated" webhook.</summary>
    [RoutePrefix("api/webshop")]
    public class WebshopController : ApiController
    {
        [HttpPost, Route("orders")]
        public async Task<HttpResponseMessage> PostOrder()
        {
            byte[] body = await Request.Content.ReadAsByteArrayAsync();
            var options = this.Options();

            string signature = Request.Headers.TryGetValues(WebhookSignature.HeaderName, out var values) ? values.FirstOrDefault() : null;
            if (!WebhookSignature.IsValid(body, signature, options.WebshopSecret))
            {
                Log.Warning("Webshop webhook with invalid signature from {Remote}", RemoteAddress());
                return Request.CreateResponse(HttpStatusCode.Unauthorized);
            }

            // The webshop sends "webhook_id=<n>" as a form post when the webhook is created or edited.
            string mediaType = Request.Content.Headers.ContentType?.MediaType;
            if (mediaType != "application/json")
            {
                return Request.CreateResponse(HttpStatusCode.OK);
            }

            WebshopOrder order;
            try
            {
                order = WebshopOrderMapper.Parse(System.Text.Encoding.UTF8.GetString(body));
            }
            catch (Exception ex) when (ex is FormatException || ex is Newtonsoft.Json.JsonException)
            {
                Log.Warning(ex, "Webshop webhook payload rejected");
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Not an order.");
            }

            if (!WebshopOrderMapper.IsFulfillable(order))
            {
                Log.Debug("Webshop order {Number} has status {Status}, not forwarded", order.Number, order.Status);
                return Request.CreateResponse(HttpStatusCode.OK);
            }

            await options.Bus.Publish<OrderSubmitted>(new
            {
                OrderId = NewId.NextGuid(),
                Channel = SalesChannels.Webshop,
                ExternalRef = order.Id.ToString(CultureInfo.InvariantCulture),
                SubmittedUtc = DateTime.UtcNow,
                Order = WebshopOrderMapper.Map(order)
            });

            Log.Information("Webshop order {Number} received", order.Number);
            return Request.CreateResponse(HttpStatusCode.Accepted);
        }

        private string RemoteAddress()
        {
            object context;
            if (Request.Properties.TryGetValue("MS_OwinContext", out context))
            {
                return ((Microsoft.Owin.IOwinContext)context).Request.RemoteIpAddress;
            }
            return "?";
        }
    }

    [RoutePrefix("api")]
    public class HealthController : ApiController
    {
        [HttpGet, Route("health")]
        public IHttpActionResult Get()
        {
            return Ok(new { status = "ok", service = "OrderHubIntake", version = typeof(HealthController).Assembly.GetName().Version.ToString() });
        }
    }
}
