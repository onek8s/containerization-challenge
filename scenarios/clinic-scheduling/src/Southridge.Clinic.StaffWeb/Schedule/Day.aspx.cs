using System;
using System.Globalization;
using System.Web.UI.WebControls;
using Southridge.Clinic.Data;
using Southridge.Clinic.Domain;
using Southridge.Clinic.Scheduling;

namespace Southridge.Clinic.StaffWeb.Schedule
{
    public partial class DayPage : ClinicPage
    {
        private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

        private DateTime SelectedDate
        {
            get
            {
                DateTime date;
                return DateTime.TryParse(DateBox.Text, Us, DateTimeStyles.None, out date) ? date.Date : DateTime.Today;
            }

            set
            {
                DateBox.Text = value.ToString("MM/dd/yyyy", Us);
            }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                SelectedDate = QueryDate("date") ?? DateTime.Today;
                ProviderList.DataSource = StaffCache.Providers(CurrentSiteId);
                ProviderList.DataBind();
                StaffProfile profile = StaffProfile.Current;
                if (profile.ProviderId.HasValue && ProviderList.Items.FindByValue(profile.ProviderId.Value.ToString()) != null)
                {
                    ProviderList.SelectedValue = profile.ProviderId.Value.ToString();
                }

                ShowCancelledBox.Checked = profile.ShowCancelled;
            }

            PrintLink.NavigateUrl = "~/Reports/Render.ashx?report=DailySchedule&SiteId=" + CurrentSiteId + "&Date=" + SelectedDate.ToString("yyyy-MM-dd");
        }

        protected void DaySource_Selecting(object sender, ObjectDataSourceSelectingEventArgs e)
        {
            e.InputParameters["siteId"] = CurrentSiteId;
            e.InputParameters["date"] = SelectedDate;
        }

        protected void Filter_Changed(object sender, EventArgs e)
        {
            DayGrid.DataBind();
        }

        protected void PreviousDay_Click(object sender, EventArgs e)
        {
            SelectedDate = SelectedDate.AddDays(SelectedDate.DayOfWeek == DayOfWeek.Monday ? -3 : -1);
            DayGrid.DataBind();
        }

        protected void NextDay_Click(object sender, EventArgs e)
        {
            SelectedDate = SelectedDate.AddDays(SelectedDate.DayOfWeek == DayOfWeek.Friday ? 3 : 1);
            DayGrid.DataBind();
        }

        protected void TodayLink_Click(object sender, EventArgs e)
        {
            SelectedDate = DateTime.Today;
            DayGrid.DataBind();
        }

        protected void DayGrid_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            if (e.Row.RowType != DataControlRowType.DataRow)
            {
                return;
            }

            var a = (DayAppointment)e.Row.DataItem;
            var status = (AppointmentStatus)a.Status;
            var label = (Label)e.Row.FindControl("StatusLabel");
            label.Text = ClinicLookups.StatusText(status) + (a.CheckedInOn.HasValue && status == AppointmentStatus.CheckedIn ? " " + a.CheckedInOn.Value.ToString("t") : string.Empty);
            label.CssClass = "status s" + a.Status;
            e.Row.CssClass = status == AppointmentStatus.Cancelled || status == AppointmentStatus.NoShow ? "dim" : string.Empty;
            e.Row.FindControl("CheckInButton").Visible = AppointmentRules.CanChange(status, AppointmentStatus.CheckedIn) && a.StartsAt.Date == DateTime.Today;
            e.Row.FindControl("RoomButton").Visible = AppointmentRules.CanChange(status, AppointmentStatus.InRoom);
            e.Row.FindControl("CompleteButton").Visible = AppointmentRules.CanChange(status, AppointmentStatus.Completed) && status != AppointmentStatus.Scheduled;
            e.Row.FindControl("NoShowButton").Visible = AppointmentRules.IsNoShow(a.StartsAt, status, DateTime.Now);
        }

        protected void DayGrid_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            int index;
            if (!int.TryParse(Convert.ToString(e.CommandArgument), out index))
            {
                return;
            }

            DataKey key = DayGrid.DataKeys[index];
            int appointmentId = (int)key["AppointmentId"];
            try
            {
                var scheduling = new SchedulingService();
                switch (e.CommandName)
                {
                    case "CheckIn":
                        CheckInAppointmentId.Value = appointmentId.ToString();
                        decimal? copay = (decimal?)key["Copay"];
                        CopayDue.Text = copay.HasValue ? copay.Value.ToString("C", Us) : "none";
                        CopayBox.Text = copay.HasValue ? copay.Value.ToString("0.00", Us) : string.Empty;
                        CheckInPatient.Text = Server.HtmlEncode(((HyperLink)DayGrid.Rows[index].FindControl("PatientLink")).Text);
                        CheckInPopup.Show();
                        return;
                    case "Room":
                        scheduling.ChangeStatus(appointmentId, AppointmentStatus.InRoom, null, UserName, null);
                        break;
                    case "Complete":
                        scheduling.ChangeStatus(appointmentId, AppointmentStatus.Completed, null, UserName, null);
                        break;
                    case "NoShow":
                        scheduling.ChangeStatus(appointmentId, AppointmentStatus.NoShow, "Marked by front desk", UserName, null);
                        break;
                }

                DayGrid.DataBind();
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }
        }

        protected void ConfirmCheckIn_Click(object sender, EventArgs e)
        {
            if (!IsValid)
            {
                CheckInPopup.Show();
                return;
            }

            try
            {
                decimal amount;
                decimal? collected = decimal.TryParse(CopayBox.Text, NumberStyles.Currency, Us, out amount) && amount > 0 ? amount : (decimal?)null;
                new SchedulingService().CheckIn(int.Parse(CheckInAppointmentId.Value), collected, UserName);
                ShowMessage("Checked in.");
                DayGrid.DataBind();
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }
        }
    }
}
