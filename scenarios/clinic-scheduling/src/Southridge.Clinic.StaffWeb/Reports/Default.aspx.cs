using System;

namespace Southridge.Clinic.StaffWeb.Reports
{
    public partial class ReportsPage : ClinicPage
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                SiteList.DataSource = StaffCache.Sites();
                SiteList.DataBind();
                SiteList.SelectedValue = CurrentSiteId.ToString();
                if (!IsInAnyRole("Billing", "PracticeManager", "Administrator"))
                {
                    ReportList.Items.Remove(ReportList.Items.FindByValue("ArAging"));
                    ReportList.Items.Remove(ReportList.Items.FindByValue("ClaimSummary"));
                }

                ReportList_SelectedIndexChanged(this, EventArgs.Empty);
            }
        }

        protected void ReportList_SelectedIndexChanged(object sender, EventArgs e)
        {
            string report = ReportList.SelectedValue;
            DateTime today = DateTime.Today;
            SiteRow.Visible = report == "DailySchedule" || report == "ProviderProductivity";
            ToRow.Visible = report == "ProviderProductivity" || report == "ClaimSummary";
            DateLabel.Text = ToRow.Visible ? "From" : (report == "ArAging" ? "As of" : "Date");
            DateBox.Text = (ToRow.Visible ? new DateTime(today.Year, today.Month, 1) : today).ToString("yyyy-MM-dd");
            ToBox.Text = today.ToString("yyyy-MM-dd");
        }

        protected void RunButton_Click(object sender, EventArgs e)
        {
            string report = ReportList.SelectedValue;
            string url = "~/Reports/Render.ashx?report=" + report + "&format=" + FormatList.SelectedValue;
            switch (report)
            {
                case "DailySchedule":
                    url += "&SiteId=" + SiteList.SelectedValue + "&Date=" + DateBox.Text;
                    break;
                case "ProviderProductivity":
                    url += "&From=" + DateBox.Text + "&To=" + ToBox.Text + "&SiteId=" + SiteList.SelectedValue;
                    break;
                case "ClaimSummary":
                    url += "&From=" + DateBox.Text + "&To=" + ToBox.Text;
                    break;
                default:
                    url += "&AsOf=" + DateBox.Text;
                    break;
            }

            Response.Redirect(url, false);
        }
    }
}
