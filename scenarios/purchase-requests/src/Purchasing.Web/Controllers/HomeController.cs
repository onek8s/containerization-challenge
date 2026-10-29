using System.Collections.Generic;
using System.Web.Mvc;
using Contoso.Purchasing.Web.Models;

namespace Contoso.Purchasing.Web.Controllers
{
    public class HomeController : PortalController
    {
        public ActionResult Index()
        {
            var user = CurrentUser;
            var model = new DashboardModel
            {
                User = user,
                MyRequests = Api.Requests("mine"),
                Approvals = user.IsManager || user.IsFinance || user.IsDirector ? Api.Requests("approvals") : new List<RequestSummary>(),
                ReadyToOrder = user.IsBuyer ? Api.Requests("ordering") : new List<RequestSummary>()
            };
            return View(model);
        }

        [AllowAnonymous]
        public ActionResult About()
        {
            return Content("Purchase Requests " + typeof(HomeController).Assembly.GetName().Version);
        }
    }
}
