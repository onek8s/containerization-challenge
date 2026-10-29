using System;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Southridge.Clinic.Infrastructure;

namespace Southridge.Clinic.PatientWeb
{
    public class Global : HttpApplication
    {
        protected void Application_Start(object sender, EventArgs e)
        {
            ClinicContainer.ApplyConfiguration(ClinicContainer.Current);
            AreaRegistration.RegisterAllAreas();
            GlobalFilters.Filters.Add(new HandleErrorAttribute());
            RegisterRoutes(RouteTable.Routes);
            ClinicLog.Info(LogCategory.General, "Patient site started ({0}).", typeof(Global).Assembly.GetName().Version);
        }

        /// <summary>The Web Forms pages are served as files; only the MyHealth area goes through MVC routing.</summary>
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");
            routes.IgnoreRoute("{resource}.aspx/{*pathInfo}");
            routes.IgnoreRoute("{resource}.asmx/{*pathInfo}");
        }

        protected void Application_Error(object sender, EventArgs e)
        {
            Exception ex = Server.GetLastError();
            var http = ex as HttpException;
            if (ex == null || (http != null && http.GetHttpCode() == 404))
            {
                return;
            }

            ErrorHandler.Handle(ex.GetBaseException(), ExceptionPolicies.UserInterface);
        }
    }
}
