using System;
using System.Data;
using System.Globalization;
using System.Web.Mvc;
using VanArsdel.Integration.Data;
using VanArsdel.Integration.Tracking.Exports;
using VanArsdel.Integration.Tracking.Security;

namespace VanArsdel.Integration.Tracking.Controllers
{
    [Authorize(Roles = TrackingRoles.CustomerService + "," + TrackingRoles.Admin)]
    public class VolumesController : TrackingController
    {
        private readonly TrackingRepository _tracking;

        public VolumesController(TrackingRepository tracking)
        {
            _tracking = tracking;
        }

        public ActionResult Index(DateTime? from, DateTime? to)
        {
            ViewBag.From = (from ?? DateTime.Today.AddDays(-30)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            ViewBag.To = (to ?? DateTime.Today).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            return View(Load(from, to));
        }

        public ActionResult Export(DateTime? from, DateTime? to)
        {
            var table = Load(from, to);
            return Excel(WorkbookExporter.Volumes(table, "Document volumes per day"), "volumes");
        }

        private DataTable Load(DateTime? from, DateTime? to)
        {
            return _tracking.DailyVolumes(StartOfDayUtc(from, 30), EndOfDayUtc(to));
        }
    }
}
