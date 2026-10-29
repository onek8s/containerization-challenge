using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web.Http;
using Wingtip.FieldSales.Api.Infrastructure;
using Wingtip.FieldSales.Api.Services;
using Wingtip.FieldSales.Contracts;

namespace Wingtip.FieldSales.Api.Controllers
{
    [RoutePrefix("api/stock"), ClientVersion]
    public class StockController : ApiController
    {
        private readonly WarehouseClient _warehouse;

        public StockController(WarehouseClient warehouse)
        {
            _warehouse = warehouse;
        }

        /// <summary>Available quantity in the distribution centre, e.g. api/stock?items=10021,20410.</summary>
        [HttpGet, Route("")]
        public Task<List<StockDto>> Get(string items)
        {
            return _warehouse.GetStockAsync((items ?? "").Split(','));
        }
    }
}
