using System;
using System.Globalization;
using System.Linq;
using System.Web.UI.WebControls;
using Southridge.Clinic.Data;
using Southridge.Clinic.Infrastructure;

namespace Southridge.Clinic.StaffWeb.Admin
{
    public partial class ApiClientsPage : ClinicPage
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                SiteList.DataSource = StaffCache.Sites();
                SiteList.DataBind();
            }
        }

        protected void AddButton_Click(object sender, EventArgs e)
        {
            if (!IsValid)
            {
                return;
            }

            if (TypeList.SelectedValue == "Kiosk" && SiteList.SelectedValue.Length == 0)
            {
                ShowError("A kiosk belongs to a site.");
                return;
            }

            string key = ApiKeys.NewKey();
            using (ClinicDataContext db = ClinicDataContextFactory.Create())
            {
                db.ExecuteCommand("INSERT dbo.ApiClient (Name, ClientType, ApiKeyHash, SiteId) VALUES ({0}, {1}, {2}, {3})",
                    NameBox.Text.Trim(), TypeList.SelectedValue, ApiKeys.Hash(key),
                    SiteList.SelectedValue.Length > 0 ? (object)int.Parse(SiteList.SelectedValue, CultureInfo.InvariantCulture) : null);
            }

            Audit("AddApiClient", "ApiClient", NameBox.Text.Trim(), null, TypeList.SelectedValue);
            ShowKey(NameBox.Text.Trim(), key);
            NameBox.Text = string.Empty;
            ClientGrid.DataBind();
        }

        protected void ClientGrid_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName != "Rotate")
            {
                return;
            }

            int id = int.Parse((string)e.CommandArgument, CultureInfo.InvariantCulture);
            string key = ApiKeys.NewKey();
            string name;
            using (ClinicDataContext db = ClinicDataContextFactory.Create())
            {
                db.ExecuteCommand("UPDATE dbo.ApiClient SET ApiKeyHash = {0} WHERE ApiClientId = {1}", ApiKeys.Hash(key), id);
                name = db.ExecuteQuery<string>("SELECT Name FROM dbo.ApiClient WHERE ApiClientId = {0}", id).Single();
            }

            Audit("RotateApiKey", "ApiClient", id.ToString(CultureInfo.InvariantCulture), null, null);
            ShowKey(name, key);
        }

        protected void ClientSource_Updated(object sender, SqlDataSourceStatusEventArgs e)
        {
            if (e.Exception == null)
            {
                Audit("EditApiClient", "ApiClient", Convert.ToString(e.Command.Parameters["@ApiClientId"].Value, CultureInfo.InvariantCulture), null, null);
            }
        }

        /// <summary>Services cache a validated key for five minutes, so a disabled client stops within that time.</summary>
        private void ShowKey(string name, string key)
        {
            KeyClient.Text = Server.HtmlEncode(name);
            KeyText.Text = key;
            KeyPanel.Visible = true;
        }
    }
}
