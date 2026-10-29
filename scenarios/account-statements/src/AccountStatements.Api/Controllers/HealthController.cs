using System;
using System.Web.Http;
using Woodgrove.Statements.Data;

namespace Woodgrove.Statements.Api.Controllers
{
    public class HealthController : ApiController
    {
        [HttpGet, Route("api/health")]
        public IHttpActionResult Get()
        {
            try
            {
                var load = new LedgerRepository(Db.FromConfig()).LastLoad();
                return Ok(new { status = "ok", server = Environment.MachineName, ledgerDate = load?.LedgerDate, ledgerLoadedAt = load?.LoadedAt });
            }
            catch (Exception ex)
            {
                return Content(System.Net.HttpStatusCode.ServiceUnavailable, new { status = "database unavailable", error = ex.GetBaseException().Message });
            }
        }
    }
}
