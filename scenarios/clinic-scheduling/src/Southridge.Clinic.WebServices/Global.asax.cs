using System;
using System.Web;
using Southridge.Clinic.Infrastructure;

namespace Southridge.Clinic.WebServices
{
    public class Global : HttpApplication
    {
        protected void Application_Start(object sender, EventArgs e)
        {
            ClinicContainer.ApplyConfiguration(ClinicContainer.Current);
            ClinicLog.Info(LogCategory.General, "Clinic web services started ({0}).", typeof(Global).Assembly.GetName().Version);
        }

        protected void Application_Error(object sender, EventArgs e)
        {
            Exception ex = Server.GetLastError();
            if (ex != null)
            {
                ClinicLog.Error(LogCategory.General, "Unhandled error in " + Request.Path, ex.GetBaseException());
            }
        }

        protected void Application_End(object sender, EventArgs e)
        {
            ClinicLog.Info(LogCategory.General, "Clinic web services stopping.");
        }
    }
}
