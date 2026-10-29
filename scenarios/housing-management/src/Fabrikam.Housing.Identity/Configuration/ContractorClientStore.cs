using System;
using System.Linq;
using System.Threading.Tasks;
using Fabrikam.Housing.Data;
using Fabrikam.Housing.Hosting.Security;
using IdentityServer4.Models;
using IdentityServer4.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace Fabrikam.Housing.Identity.Configuration
{
    /// <summary>
    /// Interactive clients from configuration plus one client-credentials client per active contractor.
    /// The contractor secret hash is maintained by the back office (Admin / Contractors / API access).
    /// </summary>
    public class ContractorClientStore : IClientStore
    {
        private readonly IConfiguration _configuration;
        private readonly HousingDbContext _db;
        private readonly IMemoryCache _cache;

        public ContractorClientStore(IConfiguration configuration, HousingDbContext db, IMemoryCache cache)
        {
            _configuration = configuration;
            _db = db;
            _cache = cache;
        }

        public async Task<Client> FindClientByIdAsync(string clientId)
        {
            var configured = IdentityConfig.Clients(_configuration).FirstOrDefault(c => c.ClientId == clientId);
            if (configured != null) return configured;
            if (string.IsNullOrEmpty(clientId) || !clientId.StartsWith("contractor-", StringComparison.OrdinalIgnoreCase)) return null;

            // Cached briefly: a rotated secret takes effect within five minutes.
            return await _cache.GetOrCreateAsync("client:" + clientId, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                var contractor = await _db.Contractors.AsNoTracking()
                    .FirstOrDefaultAsync(c => c.ApiClientId == clientId && c.Active);
                if (contractor == null || string.IsNullOrEmpty(contractor.ApiClientSecretHash)) return null;

                return new Client
                {
                    ClientId = contractor.ApiClientId,
                    ClientName = contractor.Name,
                    ClientSecrets = { new Secret(contractor.ApiClientSecretHash) },
                    AllowedGrantTypes = GrantTypes.ClientCredentials,
                    AllowedScopes = { HousingScopes.ContractorApi },
                    Claims = { new ClientClaim(HousingClaimTypes.ContractorCode, contractor.Code) },
                    AlwaysSendClientClaims = true,
                    ClientClaimsPrefix = "",
                    AccessTokenLifetime = 3600
                };
            });
        }
    }
}
