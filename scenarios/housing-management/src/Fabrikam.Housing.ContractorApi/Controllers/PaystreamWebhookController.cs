using System.IO;
using System.Text;
using System.Threading.Tasks;
using Fabrikam.Housing.Payments;
using Fabrikam.Housing.Integration;
using Fabrikam.Housing.Integration.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace Fabrikam.Housing.ContractorApi.Controllers
{
    /// <summary>
    /// Paystream posts mandate and payment events here. The jobs server also polls the events API every
    /// 15 minutes, so a missed delivery is picked up later.
    /// </summary>
    [ApiController]
    [Route("webhooks/paystream")]
    [AllowAnonymous]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class PaystreamWebhookController : ControllerBase
    {
        private readonly PaystreamEventProcessor _processor;
        private readonly IntegrationOptions _options;
        private readonly ILogger<PaystreamWebhookController> _logger;

        public PaystreamWebhookController(PaystreamEventProcessor processor, IOptions<IntegrationOptions> options, ILogger<PaystreamWebhookController> logger)
        {
            _processor = processor;
            _options = options.Value;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> Receive()
        {
            Request.EnableBuffering();
            string body;
            using (var reader = new StreamReader(Request.Body, Encoding.UTF8, false, 4096, leaveOpen: true))
                body = await reader.ReadToEndAsync();
            Request.Body.Position = 0;

            var signature = Request.Headers[PaystreamWebhookSignature.HeaderName].ToString();
            if (!PaystreamWebhookSignature.Verify(body, signature, _options.Payments.WebhookSecret))
            {
                _logger.LogWarning("Paystream webhook with an invalid signature from {Ip}", HttpContext.Connection.RemoteIpAddress);
                return Unauthorized();
            }

            try
            {
                var events = PaystreamWebhookParser.Parse(body);
                var processed = 0;
                foreach (var e in events)
                {
                    if (await _processor.ProcessAsync(e, "webhook")) processed++;
                }
                _logger.LogInformation("Paystream webhook: {Count} events, {Processed} new", events.Count, processed);
                return NoContent();
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Paystream webhook body could not be read");
                return BadRequest();
            }
        }
    }
}
