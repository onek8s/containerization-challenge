using System;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using log4net;
using Wingtip.FieldSales.Backoffice.Models;
using Wingtip.FieldSales.Backoffice.Services;
using Wingtip.FieldSales.Core;
using Wingtip.FieldSales.Data;

namespace Wingtip.FieldSales.Backoffice.Controllers
{
    [Authorize(Roles = Roles.SalesManager)]
    public class PriceListController : Controller
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(PriceListController));
        private readonly FieldSalesDbContext _db;
        private readonly SalesProcedures _procedures;
        private readonly BackofficeSettings _settings;

        public PriceListController(FieldSalesDbContext db, SalesProcedures procedures, BackofficeSettings settings)
        {
            _db = db;
            _procedures = procedures;
            _settings = settings;
        }

        public async Task<ActionResult> Index()
        {
            return View(await BuildModelAsync());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<ActionResult> Upload(HttpPostedFileBase file)
        {
            var model = await BuildModelAsync();
            if (file == null || file.ContentLength == 0)
            {
                model.ImportErrors = new[] { "Choose the CSV file to import." }.ToList();
                return View("Index", model);
            }

            // Excel saves CSV in the ANSI code page of the purchasing PC unless "CSV UTF-8" is chosen.
            PriceListImport import;
            using (var reader = new StreamReader(file.InputStream, Encoding.GetEncoding(1252), detectEncodingFromByteOrderMarks: true))
            {
                import = new PriceListCsv(_settings.PriceListCulture).Read(reader);
            }

            if (import.Errors.Count > 0 || import.Products.Count == 0)
            {
                model.ImportErrors = import.Errors.Count > 0 ? import.Errors : new[] { "The file has no products." }.ToList();
                return View("Index", model);
            }

            var result = await _procedures.ImportPriceListAsync(import.Products, Path.GetFileName(file.FileName), User.Identity.Name);
            Log.InfoFormat("{0} imported price list {1}: {2} new, {3} updated, version {4}",
                User.Identity.Name, file.FileName, result.Inserted, result.Updated, result.PriceListVersion);

            TempData["Message"] = string.Format("Price list imported: {0} new and {1} changed products. Price list version is now {2}.",
                result.Inserted, result.Updated, result.PriceListVersion);
            return RedirectToAction("Index");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<ActionResult> AddPromotion(PromotionInput input)
        {
            if (input.ValidTo < input.ValidFrom) ModelState.AddModelError("", "The promotion ends before it starts.");
            var itemNo = (input.ItemNo ?? "").Trim().ToUpperInvariant();
            if (!await _db.Products.AnyAsync(p => p.ItemNo == itemNo && p.Active)) ModelState.AddModelError("", "Unknown or inactive item " + itemNo + ".");

            if (!ModelState.IsValid)
            {
                var model = await BuildModelAsync();
                model.NewPromotion = input;
                return View("Index", model);
            }

            await _procedures.SavePromotionAsync(itemNo, input.PromoPrice, input.ValidFrom, input.ValidTo, User.Identity.Name);
            TempData["Message"] = "Promotion for " + itemNo + " saved.";
            return RedirectToAction("Index");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<ActionResult> DeletePromotion(int id)
        {
            await _procedures.DeletePromotionAsync(id);
            TempData["Message"] = "Promotion removed.";
            return RedirectToAction("Index");
        }

        private async Task<PriceListViewModel> BuildModelAsync()
        {
            var today = DateTime.Today;
            return new PriceListViewModel
            {
                PriceListVersion = await _procedures.GetPriceListVersionAsync(),
                Imports = await _db.PriceListImports.AsNoTracking().OrderByDescending(i => i.ImportedAt).Take(10).ToListAsync(),
                Rules = await _db.PriceRules.AsNoTracking().OrderBy(r => r.PriceGroup).ThenBy(r => r.Category).ToListAsync(),
                Promotions = await _db.Promotions.AsNoTracking().Where(p => p.ValidTo >= today).OrderBy(p => p.ValidFrom).ToListAsync(),
                ActiveProducts = await _db.Products.CountAsync(p => p.Active),
                NewPromotion = new PromotionInput { ValidFrom = today.AddDays(1), ValidTo = today.AddDays(14) }
            };
        }
    }
}
