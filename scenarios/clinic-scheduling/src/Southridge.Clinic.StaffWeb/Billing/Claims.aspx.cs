using System;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Web.UI.WebControls;
using Southridge.Clinic.Data;
using Southridge.Clinic.Domain;

namespace Southridge.Clinic.StaffWeb.Billing
{
    public partial class ClaimsPage : ClinicPage
    {
        private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                PayerList.DataSource = StaffCache.Payers();
                PayerList.DataBind();
                FromBox.Text = DateTime.Today.AddDays(-90).ToString("MM/dd/yyyy", Us);
                ToBox.Text = DateTime.Today.ToString("MM/dd/yyyy", Us);
            }
        }

        protected void ShowButton_Click(object sender, EventArgs e)
        {
            ClaimGrid.PageIndex = 0;
            ClaimGrid.DataBind();
        }

        protected void ClaimSource_Selecting(object sender, SqlDataSourceSelectingEventArgs e)
        {
            e.Command.Parameters["@FromDate"].Value = ParseDate(FromBox.Text, DateTime.Today.AddDays(-90));
            e.Command.Parameters["@ToDate"].Value = ParseDate(ToBox.Text, DateTime.Today);
        }

        protected void ClaimGrid_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            if (e.Row.RowType != DataControlRowType.DataRow)
            {
                return;
            }

            var row = (DataRowView)e.Row.DataItem;
            var status = (ClaimStatus)Convert.ToInt32(row["Status"], CultureInfo.InvariantCulture);
            e.Row.FindControl("ResubmitButton").Visible = status == ClaimStatus.Rejected || status == ClaimStatus.Denied;
            if (status == ClaimStatus.Rejected || status == ClaimStatus.Denied)
            {
                e.Row.CssClass = "rejected";
            }
        }

        /// <summary>
        /// A rejected claim goes back to Ready with frequency code 7 (replacement) when the payer already has it,
        /// so the next nightly batch sends it again.
        /// </summary>
        protected void ClaimGrid_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName != "Resubmit")
            {
                return;
            }

            int claimId = int.Parse((string)e.CommandArgument, CultureInfo.InvariantCulture);
            using (ClinicDataContext db = ClinicDataContextFactory.Create())
            {
                int updated = db.ExecuteCommand(
                    "UPDATE dbo.Claim SET Status = {0}, FrequencyCode = CASE WHEN PayerClaimNumber IS NULL THEN '1' ELSE '7' END, " +
                    "OriginalClaimNumber = CASE WHEN PayerClaimNumber IS NULL THEN NULL ELSE ClaimNumber END, ClaimBatchId = NULL, RejectReason = NULL " +
                    "WHERE ClaimId = {1} AND Status IN ({2}, {3})",
                    (byte)ClaimStatus.Ready, claimId, (byte)ClaimStatus.Rejected, (byte)ClaimStatus.Denied);
                if (updated == 0)
                {
                    ShowError("The claim is no longer rejected.");
                    return;
                }
            }

            Audit("ResubmitClaim", "Claim", claimId.ToString(CultureInfo.InvariantCulture), null, null);
            ShowMessage("The claim will go out in the next claim file.");
            ClaimGrid.DataBind();
        }

        protected static string StatusText(object status)
        {
            string text = ((ClaimStatus)Convert.ToInt32(status, CultureInfo.InvariantCulture)).ToString();
            return string.Concat(text.Select((c, i) => i > 0 && char.IsUpper(c) ? " " + char.ToLowerInvariant(c) : c.ToString()).ToArray());
        }

        private static DateTime ParseDate(string text, DateTime fallback)
        {
            DateTime value;
            return DateTime.TryParseExact(text, "MM/dd/yyyy", Us, DateTimeStyles.None, out value) ? value : fallback;
        }
    }
}
