using System;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;
using VanArsdel.Integration.Data;
using VanArsdel.Integration.Domain.Orders;
using VanArsdel.Integration.Monitoring;
using VanArsdel.Integration.Tracking.Exports;
using VanArsdel.Integration.Tracking.Models;
using VanArsdel.Integration.Tracking.Security;

namespace VanArsdel.Integration.Tracking.Controllers
{
    /// <summary>Partner scorecards for the monthly reviews with key accounts and suppliers.</summary>
    [Authorize(Roles = TrackingRoles.CustomerService + "," + TrackingRoles.Admin)]
    public class PartnersController : TrackingController
    {
        private readonly TrackingRepository _tracking;
        private readonly InterchangeRepository _interchanges;

        public PartnersController(TrackingRepository tracking, InterchangeRepository interchanges)
        {
            _tracking = tracking;
            _interchanges = interchanges;
        }

        public ActionResult Index(DateTime? from, DateTime? to)
        {
            return View(Build(from, to));
        }

        public ActionResult Export(DateTime? from, DateTime? to)
        {
            var model = Build(from, to);
            var title = string.Format(CultureInfo.InvariantCulture, "Partner KPIs {0:yyyy-MM-dd} - {1:yyyy-MM-dd}", model.From, model.To);
            return Excel(WorkbookExporter.PartnerKpis(model.Kpis, title), "partner-kpis");
        }

        private KpiModel Build(DateTime? from, DateTime? to)
        {
            var firstOfMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var model = new KpiModel { From = (from ?? firstOfMonth.AddMonths(-1)).Date, To = (to ?? firstOfMonth.AddDays(-1)).Date };
            var fromUtc = StartOfDayUtc(model.From, 0);
            var toUtc = EndOfDayUtc(model.To);

            var orders = _tracking.SearchOrders(new OrderSearch { FromUtc = fromUtc, ToUtc = toUtc, MaxRows = 100000 });
            var outbound = _interchanges.Search(null, fromUtc, toUtc).Where(i => i.Direction == InterchangeDirection.Outbound);
            model.Kpis = PartnerKpiCalculator.Calculate(orders, outbound).OrderBy(k => k.PartnerId).ToList();
            return model;
        }
    }
}
