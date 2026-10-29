using System.Web.Mvc;

namespace Southridge.Clinic.PatientWeb.Areas.MyHealth
{
    public class MyHealthAreaRegistration : AreaRegistration
    {
        public override string AreaName
        {
            get { return "MyHealth"; }
        }

        public override void RegisterArea(AreaRegistrationContext context)
        {
            context.MapRoute(
                "MyHealth_default",
                "MyHealth/{controller}/{action}/{id}",
                new { controller = "Appointments", action = "Index", id = UrlParameter.Optional },
                new[] { "Southridge.Clinic.PatientWeb.Areas.MyHealth.Controllers" });
        }
    }
}
