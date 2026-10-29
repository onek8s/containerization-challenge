using System.Threading.Tasks;
using System.Web.Http;
using Wingtip.FieldSales.Api.Infrastructure;
using Wingtip.FieldSales.Api.Services;
using Wingtip.FieldSales.Contracts;

namespace Wingtip.FieldSales.Api.Controllers
{
    [RoutePrefix("api/catalog"), ClientVersion]
    public class CatalogController : ApiController
    {
        private readonly CatalogService _catalog;

        public CatalogController(CatalogService catalog)
        {
            _catalog = catalog;
        }

        /// <summary>Products changed after <paramref name="since"/> (0 = everything) plus current price rules.</summary>
        [HttpGet, Route("")]
        public Task<CatalogResponse> Get(long since = 0)
        {
            return _catalog.GetChangesAsync(since);
        }
    }
}
