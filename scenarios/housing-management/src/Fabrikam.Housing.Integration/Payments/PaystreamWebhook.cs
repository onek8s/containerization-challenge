using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Fabrikam.Housing.Integration.Payments
{
    public static class PaystreamWebhookSignature
    {
        public const string HeaderName = "Webhook-Signature";

        /// <summary>The Webhook-Signature header is the hex HMAC-SHA256 of the raw request body.</summary>
        public static bool Verify(string body, string signature, string secret)
        {
            if (string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(secret) || body == null) return false;
            var expected = Compute(body, secret);
            var given = Encoding.ASCII.GetBytes(signature.Trim().ToLowerInvariant());
            var wanted = Encoding.ASCII.GetBytes(expected);
            if (given.Length != wanted.Length) return false;
            var diff = 0;
            for (var i = 0; i < wanted.Length; i++) diff |= given[i] ^ wanted[i];
            return diff == 0;
        }

        public static string Compute(string body, string secret)
        {
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret)))
            {
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
                return string.Concat(hash.Select(b => b.ToString("x2")));
            }
        }
    }

    public static class PaystreamWebhookParser
    {
        /// <summary>Parses a webhook body of the form {"events":[{...}]}.</summary>
        public static IList<PaymentEvent> Parse(string body)
        {
            var json = JObject.Parse(body);
            return ParseArray(json["events"] as JArray);
        }

        public static IList<PaymentEvent> ParseArray(JArray events)
        {
            if (events == null) return new List<PaymentEvent>();
            return events.Select(e =>
            {
                var links = e["links"] as JObject;
                var resourceType = (string)e["resource_type"];
                string resourceId = null;
                if (links != null)
                {
                    var key = resourceType == null ? null : resourceType.TrimEnd('s');
                    resourceId = (string)links[key ?? ""] ?? links.Properties().Select(p => (string)p.Value).FirstOrDefault();
                }
                return new PaymentEvent
                {
                    Id = (string)e["id"],
                    CreatedAt = e["created_at"] != null ? ((DateTime)e["created_at"]).ToLocalTime() : DateTime.Now,
                    ResourceType = resourceType,
                    Action = (string)e["action"],
                    ResourceId = resourceId,
                    Cause = (string)e["details"]?["cause"],
                    Description = (string)e["details"]?["description"]
                };
            }).ToList();
        }
    }
}
