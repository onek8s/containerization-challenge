using System;
using System.Web;
using System.Web.Routing;
using log4net;
using Microsoft.AspNet.FriendlyUrls;

namespace VanArsdel.Integration.OpsPortal
{
    public class Global : HttpApplication
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Global));

        protected void Application_Start(object sender, EventArgs e)
        {
            log4net.Config.XmlConfigurator.Configure();
            MappingConfig.Configure();
            ContainerConfig.Configure();
            RouteTable.Routes.EnableFriendlyUrls(new FriendlyUrlSettings { AutoRedirectMode = RedirectMode.Permanent });
            Log.Info("Operations portal started");
        }

        protected void Application_Error(object sender, EventArgs e)
        {
            var ex = Server.GetLastError();
            var http = ex as HttpException;
            if (http != null && http.GetHttpCode() == 404)
            {
                return;
            }

            Log.Error("Unhandled error on " + Request.RawUrl + " (" + (User == null ? "?" : User.Identity.Name) + ")", ex);
        }

        protected void Application_End(object sender, EventArgs e)
        {
            ContainerConfig.Dispose();
        }
    }
}
