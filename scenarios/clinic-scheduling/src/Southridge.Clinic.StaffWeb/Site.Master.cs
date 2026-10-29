using System;
using System.Linq;
using System.Web.Security;
using System.Web.UI;
using System.Web.UI.WebControls;
using Southridge.Clinic.Infrastructure;

namespace Southridge.Clinic.StaffWeb
{
    public partial class SiteMaster : MasterPage
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                string environment = ClinicSettings.Get("Environment", "Production");
                EnvironmentLabel.Text = environment == "Production" ? string.Empty : environment.ToUpperInvariant();
                VersionLabel.Text = typeof(SiteMaster).Assembly.GetName().Version.ToString(3);

                var siteList = UserView.FindControl("SiteList") as DropDownList;
                if (siteList != null)
                {
                    siteList.DataSource = StaffCache.Sites();
                    siteList.DataBind();
                    ListItem current = siteList.Items.FindByValue(((ClinicPage)Page).CurrentSiteId.ToString());
                    if (current != null)
                    {
                        current.Selected = true;
                    }
                }
            }
        }

        public void ShowMessage(string message, bool isError)
        {
            MessagePanel.Visible = true;
            MessagePanel.CssClass = isError ? "message error" : "message info";
            MessageText.Text = Server.HtmlEncode(message);
        }

        protected void SiteList_SelectedIndexChanged(object sender, EventArgs e)
        {
            var list = (DropDownList)sender;
            ((ClinicPage)Page).CurrentSiteId = int.Parse(list.SelectedValue);
            Response.Redirect(Request.RawUrl, false);
        }

        protected void LoginStatus_LoggingOut(object sender, LoginCancelEventArgs e)
        {
            ClinicLog.Audit(Page.User.Identity.Name, "Logout", "Signed out");
            Session.Abandon();
        }
    }
}
