using System;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using log4net;
using VanArsdel.Integration.Tracking.Security;

namespace VanArsdel.Integration.Tracking
{
    public class MvcApplication : HttpApplication
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(MvcApplication));

        protected void Application_Start()
        {
            log4net.Config.XmlConfigurator.Configure();
            MvcHandler.DisableMvcResponseHeader = true;
            UnityConfig.RegisterComponents();
            GlobalFilters.Filters.Add(new HandleErrorAttribute());
            GlobalFilters.Filters.Add(new AuthorizeAttribute());
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            Log.Info("Tracking portal started");
        }

        /// <summary>The roles and the partner scope travel in the forms ticket's UserData.</summary>
        protected void Application_PostAuthenticateRequest(object sender, EventArgs e)
        {
            TrackingPrincipal.Attach(Context);
        }

        protected void Application_Error()
        {
            var ex = Server.GetLastError();
            var http = ex as HttpException;
            if (http != null && http.GetHttpCode() == 404)
            {
                return;
            }

            Log.Error("Unhandled error on " + Request.HttpMethod + " " + Request.RawUrl, ex);
        }
    }
}
