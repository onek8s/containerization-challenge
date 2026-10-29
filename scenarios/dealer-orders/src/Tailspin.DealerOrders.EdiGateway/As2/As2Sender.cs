using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using log4net;
using Tailspin.DealerOrders.Domain;
using Tailspin.DealerOrders.Edi.As2;

namespace Tailspin.DealerOrders.EdiGateway.As2
{
    public class SendResult
    {
        public bool Delivered { get; set; }
        public string MessageId { get; set; }
        public string Disposition { get; set; }
        public string Error { get; set; }
    }

    /// <summary>Sends an outbound interchange to a partner's AS2 URL and checks the synchronous MDN.</summary>
    public class As2Sender
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(As2Sender));

        private readonly HttpClient _http;
        private readonly CertificateProvider _certificates;
        private readonly GatewaySettings _settings;

        public As2Sender(HttpClient http, CertificateProvider certificates, GatewaySettings settings)
        {
            _http = http;
            _certificates = certificates;
            _settings = settings;
        }

        public SendResult Send(EdiPartner partner, EdiMessage message)
        {
            var partnerCert = _certificates.Partner(partner);
            var codec = new As2Codec(_certificates.Own);
            var fileName = $"{message.TransactionSet}_{message.InterchangeControl}.x12";
            var outbound = codec.Pack(Encoding.ASCII.GetBytes(message.Content), "application/EDI-X12", fileName, _settings.OurAs2Id, partner.As2Id,
                partnerCert, sign: true, encrypt: partner.EncryptOutbound, mdnTo: _settings.MdnAddress, signedMdn: partner.RequestSignedMdn);

            var request = new HttpRequestMessage(HttpMethod.Post, partner.As2Url) { Content = new ByteArrayContent(outbound.Body) };
            request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(outbound.ContentType);
            foreach (var h in outbound.Headers.Where(h => !h.Key.Equals("Content-Transfer-Encoding", StringComparison.OrdinalIgnoreCase)))
                request.Headers.TryAddWithoutValidation(h.Key, h.Value);
            if (outbound.Headers.TryGetValue("Content-Transfer-Encoding", out var cte))
                request.Content.Headers.TryAddWithoutValidation("Content-Transfer-Encoding", cte);

            using (var response = _http.SendAsync(request).GetAwaiter().GetResult())
            {
                var body = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                if (!response.IsSuccessStatusCode)
                    return new SendResult { MessageId = outbound.MessageId, Error = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}" };

                var contentType = response.Content.Headers.ContentType?.ToString();
                if (body.Length == 0 || contentType == null)
                    return new SendResult { MessageId = outbound.MessageId, Error = "Partner returned no MDN." };

                var mdn = codec.ReadMdn(contentType, body, partner.RequestSignedMdn ? partnerCert : null);
                var result = new SendResult { MessageId = outbound.MessageId, Disposition = mdn.Disposition };
                if (!mdn.Processed) result.Error = "MDN: " + mdn.Disposition;
                else if (mdn.ReceivedMic != null && mdn.ReceivedMic != outbound.Mic) result.Error = "MDN MIC does not match the sent content.";
                else result.Delivered = true;
                Log.InfoFormat("AS2 {0} to {1}: {2}", outbound.MessageId, partner.Name, result.Delivered ? "delivered" : result.Error);
                return result;
            }
        }
    }
}
