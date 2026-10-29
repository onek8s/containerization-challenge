using System.Linq;
using System.Security.Claims;
using System.Web.Http;
using Wingtip.FieldSales.Api.Infrastructure;
using Wingtip.FieldSales.Contracts;

namespace Wingtip.FieldSales.Api.Controllers
{
    [RoutePrefix("api/me")]
    public class MeController : ApiController
    {
        private readonly ApiSettings _settings;

        public MeController(ApiSettings settings)
        {
            _settings = settings;
        }

        [HttpGet, Route("")]
        public MeResponse Get()
        {
            var identity = (ClaimsIdentity)User.Identity;
            return new MeResponse
            {
                UserName = User.Identity.Name,
                DisplayName = User.DisplayName(),
                RepCode = User.RepCode(),
                DiscountLimitPercent = User.DiscountLimit(),
                Roles = identity.FindAll(identity.RoleClaimType).Select(c => c.Value).ToList(),
                MinimumClientVersion = _settings.MinimumClientVersion
            };
        }
    }
}
