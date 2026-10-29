using System;
using System.Web;
using System.Web.UI;
using Tailspin.CustomerCare.Web.Security;

namespace Tailspin.CustomerCare.Web
{
    public partial class SiteMaster : MasterPage
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            var user = CareUser.Get(new HttpContextWrapper(Context));
            UserName.Text = HttpUtility.HtmlEncode(user.DisplayName);
            SupervisorMenu.Visible = user.IsSupervisor;
            AdminMenu.Visible = user.IsAdministrator;
        }

        public void ShowMessage(string text, bool isError)
        {
            MessagePanel.Visible = true;
            MessagePanel.CssClass = isError ? "message error" : "message";
            MessageText.Text = text;
        }

        protected void SearchButton_Click(object sender, EventArgs e)
        {
            var q = (SearchBox.Text ?? string.Empty).Trim();
            if (q.Length == 0) return;
            Response.Redirect("~/search?q=" + HttpUtility.UrlEncode(q), false);
            Context.ApplicationInstance.CompleteRequest();
        }
    }
}
