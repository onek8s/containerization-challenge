using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Fabrikam.Housing.Contractors;
using Fabrikam.Housing.Data;
using Fabrikam.Housing.Hosting.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Fabrikam.Housing.ContractorApi.Infrastructure
{
    /// <summary>
    /// The contractor behind the access token. Client credentials tokens carry the code as a client claim
    /// ("client_contractor_code" with the default IdentityServer prefix).
    /// </summary>
    public class CurrentContractor
    {
        private readonly IHttpContextAccessor _http;
        private readonly HousingDbContext _db;
        private Contractor _contractor;

        public CurrentContractor(IHttpContextAccessor http, HousingDbContext db)
        {
            _http = http;
            _db = db;
        }

        public static string CodeFrom(ClaimsPrincipal user)
        {
            return user?.FindFirst(HousingClaimTypes.ContractorCode)?.Value
                ?? user?.FindFirst("client_" + HousingClaimTypes.ContractorCode)?.Value;
        }

        public string ClientId => _http.HttpContext?.User.FindFirst("client_id")?.Value ?? Code;

        public string Code => CodeFrom(_http.HttpContext?.User);

        public async Task<Contractor> GetAsync()
        {
            if (_contractor != null) return _contractor;
            var code = Code;
            _contractor = await _db.Contractors.SingleOrDefaultAsync(c => c.Code == code && c.Active);
            if (_contractor == null)
                throw new Common.BusinessRuleException("CONTRACTOR", "The contractor account is not active.");
            return _contractor;
        }
    }
}
