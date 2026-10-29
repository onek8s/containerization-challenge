using System;
using System.Web.UI;

namespace Southridge.Clinic.StaffWeb.Admin
{
    public partial class AdminMaster : MasterPage
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            string page = System.IO.Path.GetFileNameWithoutExtension(Request.Path);
            foreach (Control c in new Control[] { UsersLink, TemplatesLink, SettingsLink, InterfacesLink, ApiClientsLink })
            {
                var link = (System.Web.UI.WebControls.HyperLink)c;
                if (link.NavigateUrl.EndsWith("/" + page + ".aspx", StringComparison.OrdinalIgnoreCase))
                {
                    link.CssClass = "current";
                }
            }
        }
    }
}
