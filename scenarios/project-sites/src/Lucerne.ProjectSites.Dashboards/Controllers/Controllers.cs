using System;
using System.Linq;
using System.Web.Mvc;
using Lucerne.ProjectSites.Dashboards.Infrastructure;
using Lucerne.ProjectSites.Dashboards.Services;
using Lucerne.ProjectSites.Data;
using Lucerne.ProjectSites.Documents;
using Lucerne.ProjectSites.Domain;

namespace Lucerne.ProjectSites.Dashboards.Controllers
{
    [SharePointContextFilter]
    public class HomeController : Controller
    {
        private readonly PortfolioService _portfolio;

        public HomeController(PortfolioService portfolio)
        {
            _portfolio = portfolio;
        }

        public ActionResult Index(string office, bool exceptions = false)
        {
            ViewBag.HostUrl = SharePointContextFilterAttribute.HostUrl(HttpContext);
            return View(_portfolio.GetPortfolio(office, exceptions));
        }
    }

    [SharePointContextFilter]
    public class ProjectController : Controller
    {
        private readonly PortfolioService _portfolio;
        private readonly KpiRepository _kpis;

        public ProjectController(PortfolioService portfolio, KpiRepository kpis)
        {
            _portfolio = portfolio;
            _kpis = kpis;
        }

        public ActionResult Details(string number)
        {
            if (!Project.IsValidNumber(number))
            {
                return HttpNotFound();
            }

            var model = _portfolio.GetProject(number, SharePointContextFilterAttribute.UserSid(HttpContext));
            if (model == null)
            {
                return HttpNotFound();
            }

            model.History = _kpis.History(number, DateTime.Today.AddMonths(-12));
            model.Subscribed = _kpis.SubscriptionsFor(User.Identity.Name).Any(s => s.ProjectNumber == number);
            ViewBag.HostUrl = SharePointContextFilterAttribute.HostUrl(HttpContext);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Subscribe(string number, string frequency)
        {
            if (!Project.IsValidNumber(number))
            {
                return HttpNotFound();
            }

            _kpis.Subscribe(User.Identity.Name, null, number, frequency == "Monthly" ? "Monthly" : "Weekly");
            return RedirectToRoute("Project", new { number });
        }
    }

    [SharePointContextFilter]
    public class ExportController : Controller
    {
        private const string XlsxType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        private readonly PortfolioService _portfolio;

        public ExportController(PortfolioService portfolio)
        {
            _portfolio = portfolio;
        }

        public ActionResult Portfolio(string office)
        {
            var model = _portfolio.GetPortfolio(office, false);
            return File(Workbooks.Portfolio(model.Rows, DateTime.Today), XlsxType, "portfolio-" + DateTime.Today.ToString("yyyyMMdd") + ".xlsx");
        }

        public ActionResult Register(string id)
        {
            var model = _portfolio.GetProject(id, SharePointContextFilterAttribute.UserSid(HttpContext));
            if (model == null || !model.CanSeeSite)
            {
                return HttpNotFound();
            }

            return File(Workbooks.DeliverableRegister(id, model.Deliverables, DateTime.Today), XlsxType, id + "-deliverables.xlsx");
        }
    }
}
