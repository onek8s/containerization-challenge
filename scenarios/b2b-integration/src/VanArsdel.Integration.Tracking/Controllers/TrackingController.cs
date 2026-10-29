using System;
using System.Web.Mvc;
using VanArsdel.Integration.Tracking.Security;

namespace VanArsdel.Integration.Tracking.Controllers
{
    /// <summary>Base controller: partner scoping and date range defaults.</summary>
    public abstract class TrackingController : Controller
    {
        protected TrackingPrincipal Principal
        {
            get { return TrackingPrincipal.From(User); }
        }

        protected static DateTime StartOfDayUtc(DateTime? local, int defaultDaysBack)
        {
            var day = (local ?? DateTime.Today.AddDays(-defaultDaysBack)).Date;
            return DateTime.SpecifyKind(day, DateTimeKind.Local).ToUniversalTime();
        }

        protected static DateTime EndOfDayUtc(DateTime? local)
        {
            var day = (local ?? DateTime.Today).Date.AddDays(1);
            return DateTime.SpecifyKind(day, DateTimeKind.Local).ToUniversalTime();
        }

        protected ActionResult Excel(byte[] content, string name)
        {
            return File(content, Exports.WorkbookExporter.ContentType, name + "_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".xlsx");
        }

        protected override void OnAuthorization(AuthorizationContext filterContext)
        {
            base.OnAuthorization(filterContext);
            if (filterContext.Result == null && User.Identity.IsAuthenticated && !(User is TrackingPrincipal))
            {
                filterContext.Result = RedirectToAction("Login", "Account");
            }
        }
    }
}
