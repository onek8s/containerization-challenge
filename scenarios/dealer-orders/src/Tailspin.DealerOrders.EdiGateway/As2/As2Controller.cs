using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using log4net;
using Tailspin.DealerOrders.Edi.As2;
using Tailspin.DealerOrders.Edi.X12;
using Tailspin.DealerOrders.Processing.Edi;
using Tailspin.DealerOrders.Processing.Ports;

namespace Tailspin.DealerOrders.EdiGateway.As2
{
    /// <summary>AS2 receiver. Partners post to /as2; the MDN goes back in the response unless they asked for an asynchronous one.</summary>
    [RoutePrefix("as2")]
    public class As2Controller : ApiController
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(As2Controller));

        private readonly IEdiStore _store;
        private readonly EdiOrderIntake _intake;
        private readonly CertificateProvider _certificates;
        private readonly GatewaySettings _settings;
        private readonly AsyncMdnSender _asyncMdn;

        public As2Controller(IEdiStore store, EdiOrderIntake intake, CertificateProvider certificates, GatewaySettings settings, AsyncMdnSender asyncMdn)
        {
            _store = store;
            _intake = intake;
            _certificates = certificates;
            _settings = settings;
            _asyncMdn = asyncMdn;
        }

        [HttpGet, Route("")]
        public IHttpActionResult Ping() => Ok(new { service = "Tailspin AS2", as2Id = _settings.OurAs2Id, utc = DateTime.UtcNow });

        [HttpPost, Route("")]
        public async Task<HttpResponseMessage> Receive()
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var h in Request.Headers.Concat(Request.Content.Headers))
                headers[h.Key] = string.Join(", ", h.Value);
            var body = await Request.Content.ReadAsByteArrayAsync();

            if (!headers.ContainsKey("AS2-From") || !headers.ContainsKey("Message-ID"))
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Not an AS2 message.");

            var from = headers["AS2-From"].Trim('"');
            var partner = _store.FindPartnerByAs2Id(from);
            if (partner == null || !partner.Active)
            {
                Log.WarnFormat("AS2 message {0} from unknown partner {1} refused", headers["Message-ID"], from);
                return Request.CreateErrorResponse(HttpStatusCode.Forbidden, "Unknown AS2 partner.");
            }

            var codec = new As2Codec(_certificates.Own);
            As2Inbound inbound;
            try
            {
                inbound = codec.Unpack(headers, body, id => _certificates.Partner(partner));
            }
            catch (CryptographicException ex)
            {
                Log.Error($"AS2 message {headers["Message-ID"]} from {partner.Name} failed decryption or signature check", ex);
                var failed = new As2Inbound { MessageId = headers["Message-ID"], From = from, To = _settings.OurAs2Id, MicAlgorithm = "sha-256" };
                return Mdn(codec.CreateMdn(failed, _settings.OurAs2Id, false, ex.Message.Contains("ignature") ? "authentication-failed" : "decryption-failed"));
            }

            bool processed;
            string error = null;
            try
            {
                var content = Encoding.ASCII.GetString(inbound.Payload);
                var result = _intake.Receive(content, inbound.MessageId, partner);
                Log.InfoFormat("AS2 {0} from {1}: ISA {2}, {3} received, orders {4}, rejected {5}", inbound.MessageId, partner.Name,
                    result.InterchangeControl, result.Received, string.Join(",", result.CreatedOrders), result.Rejected.Count);
                processed = true;
            }
            catch (X12FormatException ex)
            {
                Log.Warn($"AS2 {inbound.MessageId} from {partner.Name} is not a valid interchange", ex);
                processed = false;
                error = "unexpected-processing-error";
            }

            var mdn = codec.CreateMdn(inbound, _settings.OurAs2Id, processed, error);
            if (!string.IsNullOrEmpty(inbound.ReceiptDeliveryOption))
            {
                _asyncMdn.Enqueue(inbound.ReceiptDeliveryOption, mdn);
                return Request.CreateResponse(HttpStatusCode.OK);
            }
            return Mdn(mdn);
        }

        private HttpResponseMessage Mdn(As2Outbound mdn)
        {
            var response = Request.CreateResponse(HttpStatusCode.OK);
            response.Content = new ByteArrayContent(mdn.Body);
            response.Content.Headers.TryAddWithoutValidation("Content-Type", mdn.ContentType);
            foreach (var h in mdn.Headers)
                response.Headers.TryAddWithoutValidation(h.Key, h.Value);
            return response;
        }
    }

    /// <summary>Posts asynchronous MDNs in the background so the partner's request returns at once.</summary>
    public class AsyncMdnSender
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(AsyncMdnSender));
        private readonly HttpClient _http;

        public AsyncMdnSender(HttpClient http)
        {
            _http = http;
        }

        public void Enqueue(string url, As2Outbound mdn)
        {
            Task.Run(async () =>
            {
                for (var attempt = 1; attempt <= 3; attempt++)
                {
                    try
                    {
                        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new ByteArrayContent(mdn.Body) };
                        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(mdn.ContentType);
                        foreach (var h in mdn.Headers) request.Headers.TryAddWithoutValidation(h.Key, h.Value);
                        using (var response = await _http.SendAsync(request))
                        {
                            response.EnsureSuccessStatusCode();
                            Log.InfoFormat("Asynchronous MDN {0} sent to {1}", mdn.MessageId, url);
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warn($"Asynchronous MDN to {url} failed (attempt {attempt})", ex);
                        await Task.Delay(TimeSpan.FromSeconds(30 * attempt));
                    }
                }
            });
        }
    }
}
