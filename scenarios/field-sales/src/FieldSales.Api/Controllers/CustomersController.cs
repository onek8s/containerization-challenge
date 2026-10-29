using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Http;
using Wingtip.FieldSales.Api.Infrastructure;
using Wingtip.FieldSales.Contracts;
using Wingtip.FieldSales.Data;

namespace Wingtip.FieldSales.Api.Controllers
{
    [RoutePrefix("api/customers"), ClientVersion]
    public class CustomersController : ApiController
    {
        private readonly FieldSalesDbContext _db;

        public CustomersController(FieldSalesDbContext db)
        {
            _db = db;
        }

        /// <summary>Customers on the rep's own route. Sales managers get every customer.</summary>
        [HttpGet, Route("")]
        public async Task<List<CustomerDto>> Get()
        {
            IQueryable<Data.Entities.CustomerEntity> query = _db.Customers.AsNoTracking();
            if (!User.IsInRole(Roles.SalesManager))
            {
                var repCode = User.RepCode();
                query = query.Where(c => c.RepCode == repCode);
            }

            var customers = await query.OrderBy(c => c.Name).ToListAsync();
            return customers.Select(c => new CustomerDto
            {
                CustomerNo = c.CustomerNo, Name = c.Name, Street = c.Street, PostalCode = c.PostalCode, City = c.City,
                Email = c.Email, PriceGroup = c.PriceGroup, RepCode = c.RepCode, DeliveryDays = c.DeliveryDays,
                CreditBlocked = c.CreditBlocked
            }).ToList();
        }
    }
}
