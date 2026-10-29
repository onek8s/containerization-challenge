using System;
using System.Web.Http;
using Contoso.Purchasing.Api.Infrastructure;

namespace Contoso.Purchasing.Api.Controllers
{
    [RoutePrefix("api/status")]
    public class StatusController : PurchasingController
    {
        [HttpGet, Route("")]
        public object Get()
        {
            using (var connection = Db.Open())
            {
                return new
                {
                    server = Environment.MachineName,
                    database = connection.DataSource + "/" + connection.Database,
                    user = User.Identity.Name,
                    version = typeof(StatusController).Assembly.GetName().Version.ToString()
                };
            }
        }
    }
}
