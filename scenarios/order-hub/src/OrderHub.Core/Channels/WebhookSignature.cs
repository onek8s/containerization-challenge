using System;
using System.Security.Cryptography;
using System.Text;

namespace Litware.OrderHub.Core.Channels
{
    /// <summary>
    /// Webshop webhooks are signed with base64(HMAC-SHA256(body, secret)) in the
    /// X-WC-Webhook-Signature header.
    /// </summary>
    public static class WebhookSignature
    {
        public const string HeaderName = "X-WC-Webhook-Signature";

        public static string Compute(byte[] body, string secret)
        {
            if (body == null) throw new ArgumentNullException(nameof(body));
            if (string.IsNullOrEmpty(secret)) throw new ArgumentException("Secret is required.", nameof(secret));

            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret)))
            {
                return Convert.ToBase64String(hmac.ComputeHash(body));
            }
        }

        public static bool IsValid(byte[] body, string signature, string secret)
        {
            if (string.IsNullOrWhiteSpace(signature)) return false;

            byte[] expected = Encoding.ASCII.GetBytes(Compute(body, secret));
            byte[] actual = Encoding.ASCII.GetBytes(signature.Trim());
            return FixedTimeEquals(expected, actual);
        }

        private static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++)
            {
                diff |= a[i] ^ b[i];
            }
            return diff == 0;
        }
    }
}
