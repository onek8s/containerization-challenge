using System.Linq;
using System.Web.Mvc;
using CreditDesk.Web.Models;
using CreditDesk.Web.Services;

namespace CreditDesk.Web.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            var model = CreditServiceClient.Call(svc => new HomeModel
            {
                User = svc.WhoAmI(),
                MyRequests = svc.GetMyRequests().OrderByDescending(r => r.RequestedAt).Take(20).ToList(),
                PendingApprovals = svc.GetPendingApprovals().Length
            });
            return View(model);
        }

        /// <summary>Support page showing how the signed-in identity arrives at the credit server.</summary>
        public ActionResult Identity()
        {
            ViewBag.WebIdentity = System.Security.Principal.WindowsIdentity.GetCurrent();
            ViewBag.WebUser = User.Identity;
            return View(CreditServiceClient.Call(svc => svc.WhoAmI()));
        }
    }
}
