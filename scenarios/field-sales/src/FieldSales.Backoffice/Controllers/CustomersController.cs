using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using log4net;
using Wingtip.FieldSales.Backoffice.Models;
using Wingtip.FieldSales.Core;
using Wingtip.FieldSales.Data;

namespace Wingtip.FieldSales.Backoffice.Controllers
{
    public class CustomersController : Controller
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(CustomersController));
        private readonly FieldSalesDbContext _db;

        public CustomersController(FieldSalesDbContext db)
        {
            _db = db;
        }

        public async Task<ActionResult> Index(string q, string rep)
        {
            var query = _db.Customers.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(q)) query = query.Where(c => c.CustomerNo == q || c.Name.Contains(q) || c.City.Contains(q));
            if (!string.IsNullOrWhiteSpace(rep)) query = query.Where(c => c.RepCode == rep);
            ViewBag.Query = q;
            ViewBag.Rep = rep;
            return View(await query.OrderBy(c => c.Name).ToListAsync());
        }

        public async Task<ActionResult> Edit(int id)
        {
            var c = await _db.Customers.FindAsync(id);
            if (c == null) return HttpNotFound();

            var days = (DeliveryDays)c.DeliveryDays;
            return View(new CustomerEditViewModel
            {
                CustomerId = c.CustomerId, CustomerNo = c.CustomerNo, Name = c.Name, Email = c.Email, PriceGroup = c.PriceGroup,
                RepCode = c.RepCode, CreditBlocked = c.CreditBlocked,
                Monday = days.HasFlag(DeliveryDays.Monday), Tuesday = days.HasFlag(DeliveryDays.Tuesday),
                Wednesday = days.HasFlag(DeliveryDays.Wednesday), Thursday = days.HasFlag(DeliveryDays.Thursday),
                Friday = days.HasFlag(DeliveryDays.Friday),
                RepCodes = await RepCodesAsync()
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(CustomerEditViewModel model)
        {
            var c = await _db.Customers.FindAsync(model.CustomerId);
            if (c == null) return HttpNotFound();

            var days = DeliveryDays.None;
            if (model.Monday) days |= DeliveryDays.Monday;
            if (model.Tuesday) days |= DeliveryDays.Tuesday;
            if (model.Wednesday) days |= DeliveryDays.Wednesday;
            if (model.Thursday) days |= DeliveryDays.Thursday;
            if (model.Friday) days |= DeliveryDays.Friday;
            if (days == DeliveryDays.None) ModelState.AddModelError("", "Select at least one delivery day.");

            if (!ModelState.IsValid)
            {
                model.CustomerNo = c.CustomerNo;
                model.Name = c.Name;
                model.RepCodes = await RepCodesAsync();
                return View(model);
            }

            Log.InfoFormat("{0} changed customer {1}: rep {2} -> {3}, group {4} -> {5}, blocked {6} -> {7}", User.Identity.Name, c.CustomerNo,
                c.RepCode, model.RepCode, c.PriceGroup, model.PriceGroup, c.CreditBlocked, model.CreditBlocked);

            c.Email = model.Email;
            c.PriceGroup = model.PriceGroup;
            c.RepCode = string.IsNullOrWhiteSpace(model.RepCode) ? null : model.RepCode;
            c.DeliveryDays = (int)days;
            c.CreditBlocked = model.CreditBlocked;
            c.ModifiedAt = DateTime.Now;
            await _db.SaveChangesAsync();

            TempData["Message"] = "Customer " + c.CustomerNo + " saved. Sales reps get the change at their next sync.";
            return RedirectToAction("Index");
        }

        private async Task<string[]> RepCodesAsync()
        {
            var codes = await _db.Users.Where(u => u.RepCode != null && u.RepCode != "").Select(u => u.RepCode).Distinct().ToListAsync();
            return codes.OrderBy(c => c).ToArray();
        }
    }
}
