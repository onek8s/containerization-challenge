using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace VanArsdel.Integration.LabStubs.Simulators
{
    public class As2Receipt
    {
        public string MessageId { get; set; }
        public string Disposition { get; set; }
        public string ReceivedMic { get; set; }
        public bool Processed => Disposition != null && Disposition.EndsWith("processed", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// AS2 over plain HTTP as the lab speaks it (RFC 4130 headers, synchronous MDN as multipart/report). Payloads
    /// are neither signed nor encrypted here; the agreements in the BizTalk parties sign with SHA-256 and encrypt
    /// with AES, which needs the certificates from the Other People / Personal stores.
    /// </summary>
    public static class As2
    {
        public const string OurAs2Id = "VANARSDEL-AS2";

        public static string Mic(byte[] body)
        {
            return Convert.ToBase64String(SHA256.HashData(body)) + ", sha-256";
        }

        public static string ContentType(byte[] body)
        {
            var start = Encoding.ASCII.GetString(body, 0, Math.Min(body.Length, 3));
            return start == "ISA" ? "application/edi-x12" : start == "UNA" || start == "UNB" ? "application/edifact" : "application/xml";
        }

        public static As2Receipt Post(HttpClient http, string url, string from, string to, byte[] body, string fileName)
        {
            var messageId = "<" + Guid.NewGuid().ToString("N") + "@" + from.ToLowerInvariant() + ">";
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("AS2-Version", "1.2");
            request.Headers.Add("AS2-From", Quote(from));
            request.Headers.Add("AS2-To", Quote(to));
            request.Headers.Add("Message-ID", messageId);
            request.Headers.Add("Disposition-Notification-To", "b2b@vanarsdel.com");
            request.Headers.Add("Disposition-Notification-Options", "signed-receipt-protocol=optional, pkcs7-signature; signed-receipt-micalg=optional, sha-256");
            request.Content = new ByteArrayContent(body);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue(ContentType(body));
            request.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") { FileName = fileName };

            using var response = http.Send(request);
            var text = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException("AS2 POST to " + url + " returned " + (int)response.StatusCode + ": " + text);
            }

            var receipt = ParseMdn(text);
            receipt.MessageId = messageId;
            if (!receipt.Processed)
            {
                throw new InvalidOperationException("MDN disposition " + receipt.Disposition + " for " + messageId);
            }

            if (!string.Equals(receipt.ReceivedMic, Mic(body), StringComparison.Ordinal))
            {
                throw new InvalidOperationException("MDN MIC " + receipt.ReceivedMic + " does not match the payload of " + messageId);
            }

            return receipt;
        }

        public static string Mdn(string originalMessageId, string from, string to, byte[] body, string error)
        {
            var boundary = "----=_Part_" + Guid.NewGuid().ToString("N")[..12];
            var disposition = error == null ? "automatic-action/MDN-sent-automatically; processed" : "automatic-action/MDN-sent-automatically; processed/error: " + error;
            var sb = new StringBuilder()
                .Append("Content-Type: multipart/report; report-type=disposition-notification; boundary=\"").Append(boundary).Append("\"\r\n\r\n")
                .Append("--").Append(boundary).Append("\r\n")
                .Append("Content-Type: text/plain\r\n\r\n")
                .Append(error == null ? "The message was received and passed to the translator." : "The message could not be processed: " + error).Append("\r\n")
                .Append("--").Append(boundary).Append("\r\n")
                .Append("Content-Type: message/disposition-notification\r\n\r\n")
                .Append("Reporting-UA: ").Append(to).Append("\r\n")
                .Append("Original-Recipient: rfc822; ").Append(to).Append("\r\n")
                .Append("Final-Recipient: rfc822; ").Append(to).Append("\r\n")
                .Append("Original-Message-ID: ").Append(originalMessageId).Append("\r\n")
                .Append("Received-Content-MIC: ").Append(Mic(body)).Append("\r\n")
                .Append("Disposition: ").Append(disposition).Append("\r\n")
                .Append("--").Append(boundary).Append("--\r\n");
            return sb.ToString();
        }

        public static As2Receipt ParseMdn(string mdn)
        {
            var receipt = new As2Receipt();
            foreach (var line in mdn.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
            {
                if (line.StartsWith("Disposition:", StringComparison.OrdinalIgnoreCase))
                {
                    receipt.Disposition = line.Substring(12).Trim();
                }
                else if (line.StartsWith("Received-Content-MIC:", StringComparison.OrdinalIgnoreCase))
                {
                    receipt.ReceivedMic = line.Substring(21).Trim();
                }
            }

            return receipt;
        }

        public static string Unquote(string value)
        {
            return (value ?? string.Empty).Trim().Trim('"');
        }

        private static string Quote(string id)
        {
            return id.IndexOf(' ', StringComparison.Ordinal) >= 0 ? "\"" + id + "\"" : id;
        }

        public static string Describe(As2Receipt r)
        {
            return string.Format(CultureInfo.InvariantCulture, "MDN {0}, MIC ok", r.Disposition);
        }
    }
}
