using System;
using System.Web.Security;
using System.Web.UI;

namespace Southridge.Clinic.PatientWeb.Account
{
    /// <summary>Sign-out link used by the MVC pages.</summary>
    public partial class LogoutPage : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            FormsAuthentication.SignOut();
            Session.Abandon();
            Response.Redirect("~/Default.aspx", false);
            Context.ApplicationInstance.CompleteRequest();
        }
    }
}
