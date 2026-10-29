using System;
using System.Collections.Concurrent;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using VanArsdel.Integration.Data;

namespace VanArsdel.Integration.Gateway.Security
{
    public class PartnerCredential
    {
        public string PartnerId { get; set; }
        public string SecretHash { get; set; }
        public string Salt { get; set; }
        public bool Enabled { get; set; }
        public DateTime? ExpiresUtc { get; set; }
    }

    public interface IPartnerCredentialStore
    {
        PartnerCredential Find(string partnerId);
    }

    /// <summary>
    /// dbo.PartnerCredential: one shared secret per gateway partner, stored as SHA-256(salt + secret) in hex, as the
    /// 2016 partner on-boarding tool wrote it. Rows are cached for five minutes.
    /// </summary>
    public class SqlPartnerCredentialStore : IPartnerCredentialStore
    {
        private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(5);
        private readonly IntegrationDatabase _db;
        private readonly ConcurrentDictionary<string, Tuple<DateTime, PartnerCredential>> _cache = new ConcurrentDictionary<string, Tuple<DateTime, PartnerCredential>>(StringComparer.OrdinalIgnoreCase);

        public SqlPartnerCredentialStore(IntegrationDatabase db)
        {
            _db = db;
        }

        public PartnerCredential Find(string partnerId)
        {
            Tuple<DateTime, PartnerCredential> cached;
            if (_cache.TryGetValue(partnerId, out cached) && cached.Item1 > DateTime.UtcNow)
            {
                return cached.Item2;
            }

            PartnerCredential credential = null;
            using (var cmd = _db.Text("SELECT PartnerId, SecretHash, Salt, Enabled, ExpiresUtc FROM dbo.PartnerCredential WHERE PartnerId = @PartnerId"))
            {
                _db.In(cmd, "@PartnerId", DbType.String, partnerId);
                using (var r = _db.Database.ExecuteReader(cmd))
                {
                    if (r.Read())
                    {
                        credential = new PartnerCredential
                        {
                            PartnerId = IntegrationDatabase.S(r, "PartnerId"),
                            SecretHash = IntegrationDatabase.S(r, "SecretHash"),
                            Salt = IntegrationDatabase.S(r, "Salt"),
                            Enabled = IntegrationDatabase.B(r, "Enabled"),
                            ExpiresUtc = IntegrationDatabase.D(r, "ExpiresUtc")
                        };
                    }
                }
            }

            _cache[partnerId] = Tuple.Create(DateTime.UtcNow.Add(CacheFor), credential);
            return credential;
        }

        public static string HashSecret(string salt, string secret)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes((salt ?? string.Empty) + secret));
                var sb = new StringBuilder(bytes.Length * 2);
                foreach (var b in bytes)
                {
                    sb.Append(b.ToString("x2"));
                }

                return sb.ToString();
            }
        }
    }
}
