using System;
using System.Configuration;
using System.Web.UI;

namespace VanArsdel.Integration.OpsPortal
{
    public partial class SiteMaster : MasterPage
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            EnvironmentName.Text = ConfigurationManager.AppSettings["Portal.Environment"];
            UserLabel.Text = Page.User.Identity.Name + (PortalRoles.IsAdmin(Page.User) ? " (admin)" : PortalRoles.CanOperate(Page.User) ? " (operator)" : " (read only)");
            TrackingLink.NavigateUrl = ConfigurationManager.AppSettings["Portal.TrackingUrl"];

            var page = Page as OpsPage;
            var flash = page == null ? null : page.TakeFlash();
            if (!string.IsNullOrEmpty(flash))
            {
                var error = flash[0] == '!';
                FlashText.Text = error ? flash.Substring(1) : flash;
                FlashPanel.CssClass = error ? "alert alert-danger" : "alert alert-success";
                FlashPanel.Visible = true;
            }
        }
    }
}
