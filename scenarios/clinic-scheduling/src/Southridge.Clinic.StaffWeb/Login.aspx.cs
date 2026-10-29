using System;
using System.Web.Security;
using System.Web.UI.WebControls;
using Southridge.Clinic.Infrastructure;

namespace Southridge.Clinic.StaffWeb
{
    public partial class LoginPage : ClinicPage
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                int total;
                Membership.GetAllUsers(0, 1, out total);
                SetupLink.Visible = total == 0;
            }
        }

        protected void StaffLogin_LoggedIn(object sender, EventArgs e)
        {
            StaffProfile profile = StaffProfile.For(StaffLogin.UserName);
            profile.LastLogin = DateTime.Now;
            profile.Save();
            Session.Remove("CurrentSiteId");
            ClinicLog.Audit(StaffLogin.UserName, "Login", "Signed in from " + Request.UserHostAddress);
        }

        protected void StaffLogin_LoginError(object sender, EventArgs e)
        {
            ClinicLog.Warning(LogCategory.Security, "Failed sign-in for " + StaffLogin.UserName + " from " + Request.UserHostAddress);
        }
    }
}
