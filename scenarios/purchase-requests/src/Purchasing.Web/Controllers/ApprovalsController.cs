using System.Web.Mvc;
using Contoso.Purchasing.Web.Services;

namespace Contoso.Purchasing.Web.Controllers
{
    public class ApprovalsController : PortalController
    {
        public ActionResult Index()
        {
            return View(Api.Requests("approvals"));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Decide(int id, string decision, string comment)
        {
            try
            {
                var view = Api.Decide(id, decision == "approve", comment);
                Flash(view.Request.Number + (decision == "approve" ? " approved." : " rejected."));
                Session.Remove("Purchasing.User");
                return RedirectToAction("Index");
            }
            catch (ApiException ex)
            {
                Flash(ex.Message);
                return RedirectToAction("Details", "Requests", new { id });
            }
        }
    }
}
