using System;
using System.Data.Entity;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Web.Http;
using log4net;
using Wingtip.FieldSales.Data;

namespace Wingtip.FieldSales.Api.Controllers
{
    [RoutePrefix("api/status"), AllowAnonymous]
    public class StatusController : ApiController
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(StatusController));
        private readonly FieldSalesDbContext _db;

        public StatusController(FieldSalesDbContext db)
        {
            _db = db;
        }

        /// <summary>Used by the load balancer monitor and by the client to show "office reachable".</summary>
        [HttpGet, Route("")]
        public async Task<IHttpActionResult> Get()
        {
            var watch = Stopwatch.StartNew();
            try
            {
                await _db.Database.SqlQuery<int>("SELECT 1").SingleAsync();
                return Ok(new { status = "ok", server = Environment.MachineName, database = watch.ElapsedMilliseconds + " ms",
                    version = typeof(StatusController).Assembly.GetName().Version.ToString() });
            }
            catch (Exception ex)
            {
                Log.Error("Status check failed", ex);
                return Content(System.Net.HttpStatusCode.ServiceUnavailable, new { status = "database unavailable", server = Environment.MachineName });
            }
        }
    }
}
