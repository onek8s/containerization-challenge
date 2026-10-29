using System;
using System.Security.Cryptography;
using System.Text;

namespace Woodgrove.Statements.Core.Security
{
    /// <summary>Document API keys: "wgs_" + client prefix + "_" + secret. Only the SHA-256 of the key is stored.</summary>
    public static class ApiKey
    {
        public static string Generate(string clientPrefix)
        {
            if (string.IsNullOrWhiteSpace(clientPrefix)) throw new ArgumentException("Prefix is required.", nameof(clientPrefix));
            var bytes = new byte[24];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            var secret = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', 'A').Replace('/', 'B');
            return "wgs_" + clientPrefix.ToLowerInvariant() + "_" + secret;
        }

        public static string Hash(string key)
        {
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(key ?? ""));
                var sb = new StringBuilder(hash.Length * 2);
                foreach (var b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        public static string Prefix(string key)
        {
            if (string.IsNullOrEmpty(key) || !key.StartsWith("wgs_", StringComparison.Ordinal)) return null;
            var end = key.IndexOf('_', 4);
            return end > 4 ? key.Substring(4, end - 4) : null;
        }

        public static bool FixedTimeEquals(string a, string b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            var diff = 0;
            for (var i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }
}
