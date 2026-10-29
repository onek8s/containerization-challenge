using System.Web.Mvc;
using Southridge.Clinic.PatientWeb.BookingWs;

namespace Southridge.Clinic.PatientWeb.Areas.MyHealth.Controllers
{
    /// <summary>Signed-in patients only; accounts not yet linked to a record are sent to the Web Forms link page.</summary>
    [Authorize]
    public abstract class PatientControllerBase : Controller
    {
        protected PatientControllerBase(IPatientContext patientContext)
        {
            PatientContext = patientContext;
        }

        protected IPatientContext PatientContext { get; private set; }

        protected PatientSummary Patient
        {
            get { return PatientContext.Patient; }
        }

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (filterContext.HttpContext.User.Identity.IsAuthenticated && Patient == null)
            {
                filterContext.Result = Redirect(Url.Content("~/Account/Link.aspx?ReturnUrl=" + Server.UrlEncode(Request.RawUrl)));
                return;
            }

            ViewBag.PatientName = Patient == null ? null : Patient.FirstName + " " + Patient.LastName;
            base.OnActionExecuting(filterContext);
        }

        protected override void OnException(ExceptionContext filterContext)
        {
            var booking = filterContext.Exception as BookingException;
            if (booking != null && !filterContext.ExceptionHandled)
            {
                filterContext.ExceptionHandled = true;
                filterContext.Result = View("Unavailable", (object)booking.Message);
                return;
            }

            base.OnException(filterContext);
        }
    }
}
