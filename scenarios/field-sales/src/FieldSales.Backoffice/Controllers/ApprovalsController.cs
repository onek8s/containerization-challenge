using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using log4net;
using Wingtip.FieldSales.Backoffice.Models;
using Wingtip.FieldSales.Core;
using Wingtip.FieldSales.Data;
using Wingtip.FieldSales.Data.Mail;

namespace Wingtip.FieldSales.Backoffice.Controllers
{
    [Authorize(Roles = Roles.SalesManager)]
    public class ApprovalsController : Controller
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(ApprovalsController));
        private readonly FieldSalesDbContext _db;
        private readonly SalesProcedures _procedures;
        private readonly ConfirmationMailer _mailer;

        public ApprovalsController(FieldSalesDbContext db, SalesProcedures procedures, ConfirmationMailer mailer)
        {
            _db = db;
            _procedures = procedures;
            _mailer = mailer;
        }

        public async Task<ActionResult> Index()
        {
            var pending = (int)OrderStatus.PendingApproval;
            var orders = await _db.SalesOrders.AsNoTracking().Include(o => o.Customer)
                .Where(o => o.Status == pending).OrderBy(o => o.ReceivedAt).ToListAsync();
            return View(orders);
        }

        public async Task<ActionResult> Details(int id)
        {
            var order = await _db.SalesOrders.AsNoTracking().Include(o => o.Customer).Include(o => o.Lines)
                .SingleOrDefaultAsync(o => o.SalesOrderId == id);
            if (order == null) return HttpNotFound();
            return View(order);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<ActionResult> Decide(DecisionViewModel model)
        {
            if (!model.Approve && string.IsNullOrWhiteSpace(model.Comment))
            {
                TempData["Error"] = "Give the reason for the rejection; the sales rep sees it in Field Sales.";
                return RedirectToAction("Details", new { id = model.SalesOrderId });
            }

            var result = await _procedures.DecideOrderAsync(model.SalesOrderId, model.Approve, User.Identity.Name, model.Comment);
            Log.InfoFormat("{0} {1} order {2}", User.Identity.Name, model.Approve ? "approved" : "rejected", result.OrderNo);

            if (result.Status == (int)OrderStatus.Confirmed)
            {
                try
                {
                    var sent = await _mailer.SendAsync(model.SalesOrderId);
                    TempData["Message"] = "Order " + result.OrderNo + " approved." + (sent ? " Confirmation sent to " + result.CustomerEmail + "." : "");
                }
                catch (Exception ex)
                {
                    Log.Error("Confirmation mail for " + result.OrderNo + " failed", ex);
                    TempData["Message"] = "Order " + result.OrderNo + " approved, but the confirmation e-mail could not be sent.";
                }
            }
            else
            {
                TempData["Message"] = "Order " + result.OrderNo + " rejected.";
            }

            return RedirectToAction("Index");
        }
    }
}
