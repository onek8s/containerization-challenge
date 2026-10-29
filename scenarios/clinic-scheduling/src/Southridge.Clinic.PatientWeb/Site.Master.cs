using System;
using System.Web.UI;

namespace Southridge.Clinic.PatientWeb
{
    public partial class SiteMaster : MasterPage
    {
        public void ShowMessage(string text, bool error)
        {
            MessageText.Text = text;
            MessagePanel.CssClass = error ? "message error" : "message";
            MessagePanel.Visible = true;
        }

        protected void Status_LoggingOut(object sender, System.Web.UI.WebControls.LoginCancelEventArgs e)
        {
            Session.Abandon();
        }
    }
}
