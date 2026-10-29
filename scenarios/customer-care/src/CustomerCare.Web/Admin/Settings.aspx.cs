using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Web.UI.WebControls;
using Tailspin.CustomerCare.Data;
using Tailspin.CustomerCare.Data.Repositories;
using Tailspin.CustomerCare.Domain;
using Tailspin.CustomerCare.Web.Security;
using Tailspin.CustomerCare.Web.Services;

namespace Tailspin.CustomerCare.Web.Admin
{
    public partial class SettingsPage : CarePage
    {
        private readonly AdminRepository _admin = new AdminRepository();

        protected override CareRole RequiredRole
        {
            get { return CareRole.Administrator; }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack) Bind();

            ServerName.Text = Environment.MachineName;
            TimeZoneName.Text = CareSettings.TimeZone.DisplayName;
            OfficeHours.Text = CareSettings.OfficeOpens.ToString(@"hh\:mm") + " - " + CareSettings.OfficeCloses.ToString(@"hh\:mm");
            StartedAt.Text = LocalTime.Format(Process.GetCurrentProcess().StartTime.ToUniversalTime());
            SessionCount.Text = (Application["ActiveSessions"] ?? "n/a").ToString();
        }

        protected void SaveTargetsButton_Click(object sender, EventArgs e)
        {
            var targets = new List<SlaTarget>();
            foreach (RepeaterItem item in Targets.Items)
            {
                double response, resolve;
                var priority = (CasePriority)int.Parse(((HiddenField)item.FindControl("TargetPriority")).Value);
                if (!double.TryParse(((TextBox)item.FindControl("ResponseHours")).Text, NumberStyles.Float, CultureInfo.InvariantCulture, out response) ||
                    !double.TryParse(((TextBox)item.FindControl("ResolveHours")).Text, NumberStyles.Float, CultureInfo.InvariantCulture, out resolve))
                {
                    ShowMessage("Hours must be numbers, for example 4 or 0.5.", true);
                    return;
                }

                targets.Add(new SlaTarget { Priority = priority, ResponseHours = response, ResolveHours = resolve });
            }

            if (Try(() => _admin.SaveSlaTargets(targets))) ShowMessage("Targets saved.");
            Bind();
        }

        protected void HolidayGrid_RowDeleting(object sender, GridViewDeleteEventArgs e)
        {
            _admin.DeleteHoliday((DateTime)HolidayGrid.DataKeys[e.RowIndex].Value);
            Bind();
        }

        protected void AddHolidayButton_Click(object sender, EventArgs e)
        {
            DateTime date;
            if (!DateTime.TryParseExact(HolidayDate.Text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date) || string.IsNullOrWhiteSpace(HolidayName.Text))
            {
                ShowMessage("Enter the date and the name of the holiday.", true);
                return;
            }

            if (Try(() => _admin.AddHoliday(date, HolidayName.Text.Trim())))
            {
                HolidayDate.Text = HolidayName.Text = string.Empty;
            }

            Bind();
        }

        protected void ClearCacheButton_Click(object sender, EventArgs e)
        {
            ReferenceData.Invalidate();
            ShowMessage("Reference data will be read again from the database.");
        }

        private void Bind()
        {
            Targets.DataSource = _admin.SlaTargets();
            Targets.DataBind();
            HolidayGrid.DataSource = _admin.Holidays();
            HolidayGrid.DataBind();
        }
    }
}
