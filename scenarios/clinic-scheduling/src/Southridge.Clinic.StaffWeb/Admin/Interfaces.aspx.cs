using System;
using System.Globalization;
using System.Web.UI.WebControls;
using Southridge.Clinic.Data;
using Southridge.Clinic.Data.Repositories;
using Southridge.Clinic.Domain;

namespace Southridge.Clinic.StaffWeb.Admin
{
    public partial class InterfacesPage : ClinicPage
    {
        private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                FromBox.Text = DateTime.Today.AddDays(-1).ToString("MM/dd/yyyy", Us);
                ToBox.Text = DateTime.Today.ToString("MM/dd/yyyy", Us);
                string status = Request.QueryString["status"];
                if (status != null && StatusList.Items.FindByValue(status) != null)
                {
                    StatusList.SelectedValue = status;
                    FromBox.Text = DateTime.Today.AddDays(-7).ToString("MM/dd/yyyy", Us);
                }
            }
        }

        protected void ShowButton_Click(object sender, EventArgs e)
        {
            PayloadPanel.Visible = false;
            MessageGrid.PageIndex = 0;
            MessageGrid.DataBind();
        }

        protected void MessageSource_Selecting(object sender, ObjectDataSourceSelectingEventArgs e)
        {
            e.InputParameters["fromDate"] = ParseDate(FromBox.Text, DateTime.Today.AddDays(-1));
            e.InputParameters["toDate"] = ParseDate(ToBox.Text, DateTime.Today);
        }

        protected void MessageGrid_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            if (e.Row.RowType != DataControlRowType.DataRow)
            {
                return;
            }

            var row = (MessageLogRow)e.Row.DataItem;
            bool canResend = row.Direction == (byte)MessageDirection.Outbound && row.Status == (byte)MessageStatus.Error;
            e.Row.FindControl("ResendButton").Visible = canResend;
            if (row.Status == (byte)MessageStatus.Error)
            {
                e.Row.CssClass = "rejected";
            }
        }

        protected void MessageGrid_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName != "View" && e.CommandName != "Resend")
            {
                return;
            }

            int messageId = int.Parse((string)e.CommandArgument, CultureInfo.InvariantCulture);
            var repository = new InterfaceRepository();
            if (e.CommandName == "Resend")
            {
                repository.Resend(messageId, UserName);
                MessageGrid.DataBind();
                ShowMessage("Message " + messageId + " queued again.");
                return;
            }

            string ack;
            string payload = repository.GetPayload(messageId, out ack);
            PayloadTitle.Text = messageId.ToString(CultureInfo.InvariantCulture);
            PayloadText.Text = payload == null ? "(not found)" : payload.Replace('\r', '\n');
            AckText.Text = ack == null ? "(none)" : ack.Replace('\r', '\n');
            PayloadPanel.Visible = true;
            Audit("ViewMessage", "InterfaceMessage", PayloadTitle.Text, null, null);
        }

        private static DateTime ParseDate(string text, DateTime fallback)
        {
            DateTime value;
            return DateTime.TryParseExact(text, "MM/dd/yyyy", Us, DateTimeStyles.None, out value) ? value : fallback;
        }
    }
}
