using System.Web.Mvc;

namespace Woodgrove.Statements.Web.Controllers
{
    public class DeliveriesController : Controller
    {
        public ActionResult Index()
        {
            return View(PortalServices.Deliveries.Problems(200));
        }

        [HttpPost, ValidateAntiForgeryToken, PortalRole(PortalRoles.CreditControl)]
        public ActionResult Resend(long id)
        {
            PortalServices.Deliveries.Resend(id);
            TempData["Message"] = "Delivery queued for resending.";
            return RedirectToAction("Index");
        }
    }
}
