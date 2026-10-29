using System.Linq;
using System.Web.Mvc;
using Woodgrove.Statements.Web.Models;

namespace Woodgrove.Statements.Web.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            var subscriptions = PortalServices.Subscriptions;
            var model = new DashboardModel
            {
                LastLoad = PortalServices.Ledger.LastLoad(),
                RecentRuns = subscriptions.Runs(null, 10),
                Problems = PortalServices.Deliveries.Problems(10),
                Upcoming = subscriptions.List().Where(s => s.Enabled && s.NextRunAt.HasValue).OrderBy(s => s.NextRunAt).Take(8).ToList()
            };
            return View(model);
        }
    }
}
