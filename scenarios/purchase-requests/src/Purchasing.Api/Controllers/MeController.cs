using System.Web.Http;
using Contoso.Purchasing.Api.Infrastructure;
using Contoso.Purchasing.Core;

namespace Contoso.Purchasing.Api.Controllers
{
    [RoutePrefix("api/me")]
    public class MeController : PurchasingController
    {
        [HttpGet, Route("")]
        public UserContext Get()
        {
            return CurrentUser;
        }
    }
}
