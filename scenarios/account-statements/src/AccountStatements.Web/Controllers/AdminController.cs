using System.Linq;
using System.Web.Mvc;
using Woodgrove.Statements.Core.Model;
using Woodgrove.Statements.Core.Reports;
using Woodgrove.Statements.Core.Security;
using Woodgrove.Statements.Web.Models;

namespace Woodgrove.Statements.Web.Controllers
{
    [PortalRole(PortalRoles.Admin)]
    public class AdminController : Controller
    {
        public ActionResult ApiClients()
        {
            ViewBag.ReportCodes = string.Join(", ", ReportDefinitionInfo.All.Where(r => r.Code != ReportCodes.StatementRun).Select(r => r.Code));
            return View(PortalServices.ApiClients.List());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult CreateClient(ApiClientCreateModel model)
        {
            if (ModelState.IsValid && PortalServices.ApiClients.FindByPrefix(model.Prefix) != null)
                ModelState.AddModelError("Prefix", "The prefix is already used.");
            if (!ModelState.IsValid)
            {
                TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return RedirectToAction("ApiClients");
            }
            var key = ApiKey.Generate(model.Prefix);
            PortalServices.ApiClients.Create(new ApiClient
            {
                Name = model.Name.Trim(), KeyPrefix = model.Prefix, KeyHash = ApiKey.Hash(key),
                AllowedReports = model.AllowedReports.Trim().ToUpperInvariant(), CreatedBy = User.Identity.Name
            });
            return View("KeyCreated", new ApiKeyCreatedModel { Name = model.Name, Key = key });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult NewKey(int id)
        {
            var client = PortalServices.ApiClients.List().FirstOrDefault(c => c.ClientId == id);
            if (client == null) return HttpNotFound();
            var key = ApiKey.Generate(client.KeyPrefix);
            PortalServices.ApiClients.ReplaceKey(id, ApiKey.Hash(key));
            return View("KeyCreated", new ApiKeyCreatedModel { Name = client.Name, Key = key });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult SetEnabled(int id, bool enabled)
        {
            PortalServices.ApiClients.SetEnabled(id, enabled);
            return RedirectToAction("ApiClients");
        }
    }
}
