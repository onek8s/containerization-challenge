using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Owin.Security.Infrastructure;
using Wingtip.FieldSales.Api.Infrastructure;
using Wingtip.FieldSales.Data;
using Wingtip.FieldSales.Data.Entities;

namespace Wingtip.FieldSales.Api.Auth
{
    /// <summary>
    /// Refresh tokens are random values given to the client; the database keeps only their hash together with the
    /// protected ticket. Each token can be used once and is replaced by a new one (rotation).
    /// </summary>
    public class RefreshTokenProvider : IAuthenticationTokenProvider
    {
        private readonly Func<FieldSalesDbContext> _dbFactory;
        private readonly ApiSettings _settings;

        public RefreshTokenProvider(Func<FieldSalesDbContext> dbFactory, ApiSettings settings)
        {
            _dbFactory = dbFactory;
            _settings = settings;
        }

        public async Task CreateAsync(AuthenticationTokenCreateContext context)
        {
            string clientId;
            if (!context.Ticket.Properties.Dictionary.TryGetValue("as:client_id", out clientId) || string.IsNullOrEmpty(clientId)) return;

            var bytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            var token = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

            var now = DateTime.UtcNow;
            context.Ticket.Properties.IssuedUtc = now;
            context.Ticket.Properties.ExpiresUtc = now.Add(_settings.RefreshTokenLifetime);

            using (var db = _dbFactory())
            {
                var userName = context.Ticket.Identity.Name;
                var expired = db.RefreshTokens.Where(t => t.UserName == userName && t.ExpiresUtc < now).ToList();
                db.RefreshTokens.RemoveRange(expired);

                db.RefreshTokens.Add(new RefreshTokenEntity
                {
                    TokenHash = Hash(token),
                    UserName = userName,
                    ClientId = clientId,
                    IssuedUtc = now,
                    ExpiresUtc = now.Add(_settings.RefreshTokenLifetime),
                    ProtectedTicket = context.SerializeTicket()
                });
                await db.SaveChangesAsync();
            }

            context.SetToken(token);
        }

        public async Task ReceiveAsync(AuthenticationTokenReceiveContext context)
        {
            var hash = Hash(context.Token);
            using (var db = _dbFactory())
            {
                var stored = await db.RefreshTokens.FindAsync(hash);
                if (stored == null) return;

                db.RefreshTokens.Remove(stored);
                await db.SaveChangesAsync();

                if (stored.ExpiresUtc > DateTime.UtcNow) context.DeserializeTicket(stored.ProtectedTicket);
            }
        }

        public void Create(AuthenticationTokenCreateContext context)
        {
            throw new NotSupportedException();
        }

        public void Receive(AuthenticationTokenReceiveContext context)
        {
            throw new NotSupportedException();
        }

        private static string Hash(string token)
        {
            using (var sha = SHA256.Create())
            {
                return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(token)));
            }
        }
    }
}
