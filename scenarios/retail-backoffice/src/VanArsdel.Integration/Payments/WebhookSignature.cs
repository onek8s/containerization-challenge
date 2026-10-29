using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace VanArsdel.Integration.Payments
{
    /// <summary>
    /// Verifies the "Psp-Signature: t=1650000000,v1=hex(hmac_sha256(secret, t + '.' + body))" header.
    /// </summary>
    public static class WebhookSignature
    {
        public static string Create(string secret, long timestamp, string body)
        {
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret)))
            {
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(timestamp.ToString(CultureInfo.InvariantCulture) + "." + body));
                return "t=" + timestamp.ToString(CultureInfo.InvariantCulture) + ",v1=" + ToHex(hash);
            }
        }

        public static bool Verify(string secret, string header, string body, DateTimeOffset now, TimeSpan tolerance)
        {
            if (string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(header)) return false;

            long timestamp = 0;
            string signature = null;
            foreach (var part in header.Split(','))
            {
                var kv = part.Split(new[] { '=' }, 2);
                if (kv.Length != 2) continue;
                if (kv[0].Trim() == "t") long.TryParse(kv[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out timestamp);
                if (kv[0].Trim() == "v1") signature = kv[1].Trim();
            }
            if (timestamp == 0 || signature == null) return false;
            if (Math.Abs(now.ToUnixTimeSeconds() - timestamp) > tolerance.TotalSeconds) return false;

            var expected = Create(secret, timestamp, body);
            var expectedSig = expected.Substring(expected.IndexOf("v1=", StringComparison.Ordinal) + 3);
            return FixedTimeEquals(expectedSig, signature);
        }

        private static bool FixedTimeEquals(string a, string b)
        {
            if (a.Length != b.Length) return false;
            var diff = 0;
            for (var i = 0; i < a.Length; i++) diff |= a[i] ^ char.ToLowerInvariant(b[i]);
            return diff == 0;
        }

        private static string ToHex(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
            return sb.ToString();
        }
    }
}
