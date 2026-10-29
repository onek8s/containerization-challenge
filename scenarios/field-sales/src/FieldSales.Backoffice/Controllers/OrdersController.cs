using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using Wingtip.FieldSales.Backoffice.Models;
using Wingtip.FieldSales.Data;
using Wingtip.FieldSales.Data.Entities;

namespace Wingtip.FieldSales.Backoffice.Controllers
{
    public class OrdersController : Controller
    {
        private readonly FieldSalesDbContext _db;

        public OrdersController(FieldSalesDbContext db)
        {
            _db = db;
        }

        public async Task<ActionResult> Index(OrderSearchViewModel model)
        {
            var from = (model.From ?? DateTime.Today.AddDays(-7)).Date;
            var to = (model.To ?? DateTime.Today).Date.AddDays(1);
            model.From = from;
            model.To = to.AddDays(-1);

            IQueryable<SalesOrderEntity> query = _db.SalesOrders.AsNoTracking().Include(o => o.Customer)
                .Where(o => o.ReceivedAt >= from && o.ReceivedAt < to);
            if (!string.IsNullOrWhiteSpace(model.RepCode)) query = query.Where(o => o.RepCode == model.RepCode);
            if (!string.IsNullOrWhiteSpace(model.Customer))
                query = query.Where(o => o.CustomerNo == model.Customer || o.Customer.Name.Contains(model.Customer));
            if (model.Status.HasValue) query = query.Where(o => o.Status == model.Status.Value);

            model.Orders = await query.OrderByDescending(o => o.ReceivedAt).Take(1000).ToListAsync();
            return View(model);
        }

        public async Task<ActionResult> Details(int id)
        {
            var order = await _db.SalesOrders.AsNoTracking().Include(o => o.Customer).Include(o => o.Lines)
                .SingleOrDefaultAsync(o => o.SalesOrderId == id);
            if (order == null) return HttpNotFound();
            return View(order);
        }
    }
}
