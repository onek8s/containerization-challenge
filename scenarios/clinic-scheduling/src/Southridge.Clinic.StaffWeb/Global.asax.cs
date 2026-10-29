using System;
using System.Web;
using System.Web.Security;
using Southridge.Clinic.Infrastructure;
using Southridge.Clinic.Referrals;

namespace Southridge.Clinic.StaffWeb
{
    public class Global : HttpApplication
    {
        protected void Application_Start(object sender, EventArgs e)
        {
            ClinicContainer.ApplyConfiguration(ClinicContainer.Current);
            ReferralSessions.Initialize("web");
            EnsureRoles();
            ClinicLog.Info(LogCategory.General, "Staff site started ({0}).", typeof(Global).Assembly.GetName().Version);
        }

        protected void Application_Error(object sender, EventArgs e)
        {
            Exception ex = Server.GetLastError();
            if (ex == null)
            {
                return;
            }

            var http = ex as HttpException;
            if (http != null && http.GetHttpCode() == 404)
            {
                return;
            }

            // ELMAH records the error as well (ErrorLogModule); this puts it in the application log.
            ErrorHandler.Handle(ex.GetBaseException(), ExceptionPolicies.UserInterface);
        }

        protected void Session_Start(object sender, EventArgs e)
        {
            Session["StartedAt"] = DateTime.Now;
        }

        /// <summary>Roles are created on start so a new database only needs the first administrator (Setup.aspx).</summary>
        private static void EnsureRoles()
        {
            try
            {
                foreach (string role in ClinicRoles.StaffRoles)
                {
                    if (!Roles.RoleExists(role))
                    {
                        Roles.CreateRole(role);
                    }
                }

                if (!Roles.RoleExists(ClinicRoles.ServiceAccount))
                {
                    Roles.CreateRole(ClinicRoles.ServiceAccount);
                }
            }
            catch (Exception ex)
            {
                ClinicLog.Error(LogCategory.Security, "Could not create the application roles.", ex);
            }
        }
    }
}
