using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using Wingtip.FieldSales.Backoffice.Models;
using Wingtip.FieldSales.Backoffice.Services;
using Wingtip.FieldSales.Core;
using Wingtip.FieldSales.Data;

namespace Wingtip.FieldSales.Backoffice.Controllers
{
    public class HomeController : Controller
    {
        private readonly FieldSalesDbContext _db;
        private readonly SalesProcedures _procedures;
        private readonly BackofficeSettings _settings;

        public HomeController(FieldSalesDbContext db, SalesProcedures procedures, BackofficeSettings settings)
        {
            _db = db;
            _procedures = procedures;
            _settings = settings;
        }

        public async Task<ActionResult> Index()
        {
            var today = DateTime.Today;
            var weekAgo = today.AddDays(-7);
            var pending = (int)OrderStatus.PendingApproval;
            var cancelled = (int)OrderStatus.Cancelled;
            var rejected = (int)OrderStatus.Rejected;

            var model = new DashboardViewModel
            {
                PendingApprovals = await _db.SalesOrders.CountAsync(o => o.Status == pending),
                OrdersToday = await _db.SalesOrders.CountAsync(o => o.ReceivedAt >= today),
                NetToday = await _db.SalesOrders.Where(o => o.ReceivedAt >= today && o.Status != cancelled && o.Status != rejected)
                    .Select(o => (decimal?)o.NetTotal).SumAsync() ?? 0m,
                LastPriceListImport = await _db.PriceListImports.Select(i => (DateTime?)i.ImportedAt).MaxAsync(),
                PriceListVersion = await _procedures.GetPriceListVersionAsync(),
                Reps = await _db.SalesOrders.Where(o => o.ReceivedAt >= weekAgo)
                    .GroupBy(o => new { o.RepCode, o.SubmittedBy })
                    .Select(g => new RepActivity
                    {
                        RepCode = g.Key.RepCode, SubmittedBy = g.Key.SubmittedBy, Orders = g.Count(),
                        NetTotal = g.Sum(o => o.NetTotal), LastReceived = g.Max(o => o.ReceivedAt)
                    })
                    .OrderBy(r => r.RepCode).ToListAsync(),
                ClientInstallUrl = _settings.ClientInstallUrl
            };
            return View(model);
        }
    }
}
