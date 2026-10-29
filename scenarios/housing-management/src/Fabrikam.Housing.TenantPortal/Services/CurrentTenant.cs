using System;
using System.Linq;
using System.Threading.Tasks;
using Fabrikam.Housing.Data;
using Fabrikam.Housing.Hosting.Security;
using Fabrikam.Housing.Tenancies;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Fabrikam.Housing.TenantPortal.Services
{
    /// <summary>The signed-in tenant with tenancy and property, loaded once per request.</summary>
    public class CurrentTenant
    {
        private readonly IHttpContextAccessor _http;
        private readonly HousingDbContext _db;
        private Tenant _tenant;

        public CurrentTenant(IHttpContextAccessor http, HousingDbContext db)
        {
            _http = http;
            _db = db;
        }

        public int TenantId
        {
            get
            {
                var value = _http.HttpContext?.User.FindFirst(HousingClaimTypes.TenantId)?.Value;
                if (!int.TryParse(value, out var id)) throw new UnauthorizedAccessException("No tenant on the signed-in account.");
                return id;
            }
        }

        public async Task<Tenant> GetAsync()
        {
            if (_tenant != null) return _tenant;
            _tenant = await _db.Tenants
                .Include(t => t.Tenancy).ThenInclude(t => t.Property).ThenInclude(p => p.Estate)
                .FirstOrDefaultAsync(t => t.Id == TenantId);
            if (_tenant == null || _tenant.Tenancy.Status == TenancyStatus.Ended)
                throw new UnauthorizedAccessException("The tenancy for this account has ended.");
            return _tenant;
        }

        public async Task<Tenancy> TenancyAsync() => (await GetAsync()).Tenancy;
    }
}
