using System;
using Southridge.Clinic.Infrastructure;

namespace Southridge.Clinic.PatientWeb.Account
{
    public partial class LoginPage : PortalPage
    {
        protected void PatientLogin_LoggedIn(object sender, EventArgs e)
        {
            Session.Remove("Portal.Patient");
            ClinicLog.Audit("portal:" + PatientLogin.UserName, "PortalLogin", Request.UserHostAddress);
        }
    }
}
