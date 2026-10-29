using System;
using System.Globalization;
using System.Linq;
using Southridge.Clinic.Data;
using Southridge.Clinic.Data.Repositories;
using Southridge.Clinic.Domain;
using Southridge.Clinic.Scheduling;

namespace Southridge.Clinic.StaffWeb.Schedule
{
    public partial class AppointmentPage : ClinicPage
    {
        private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

        protected int AppointmentId
        {
            get { return QueryInt("id") ?? 0; }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                Bind();
            }
        }

        protected void RescheduleButton_Click(object sender, EventArgs e)
        {
            DateTime when;
            if (!DateTime.TryParse(NewTimeBox.Text, Us, DateTimeStyles.None, out when))
            {
                ShowError("Enter the new time as MM/dd/yyyy h:mm AM.");
                return;
            }

            try
            {
                BookingResult result = new SchedulingService().Reschedule(AppointmentId, when, null, null, UserName, AppointmentSource.Staff);
                Response.Redirect("~/Schedule/Appointment.aspx?id=" + result.AppointmentId, false);
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }
        }

        protected void CancelButton_Click(object sender, EventArgs e)
        {
            try
            {
                new SchedulingService().Cancel(AppointmentId, CancelReasonList.SelectedValue, UserName);
                ShowMessage("The appointment was cancelled.");
                Bind();
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }
        }

        private void Bind()
        {
            using (ClinicDataContext db = ClinicDataContextFactory.CreateReadOnly())
            {
                Appointment a = db.Appointments.SingleOrDefault(x => x.AppointmentId == AppointmentId);
                if (a == null)
                {
                    ShowError("Appointment " + AppointmentId + " was not found.");
                    ActionsPanel.Visible = false;
                    return;
                }

                var status = (AppointmentStatus)a.Status;
                CodeLabel.Text = a.ConfirmationCode;
                var row = new
                {
                    a.PatientId,
                    PatientName = a.Patient.LastName + ", " + a.Patient.FirstName,
                    a.Patient.Mrn,
                    a.StartsAt,
                    Minutes = (int)(a.EndsAt - a.StartsAt).TotalMinutes,
                    ProviderName = ClinicLookups.ProviderName(a.Provider),
                    SiteName = a.Site.Name,
                    TypeName = a.AppointmentType.Name,
                    a.Reason,
                    StatusText = ClinicLookups.StatusText(status),
                    BookedText = a.BookedOn.ToString("MM/dd/yyyy h:mm tt", Us) + " by " + a.BookedBy,
                    ReferralNumber = a.ReferralId.HasValue ? "#" + a.ReferralId.Value : null,
                    CancelText = a.CancelledOn.HasValue ? a.CancelledOn.Value.ToString("g", Us) + " by " + a.CancelledBy + ": " + a.CancelReason + (a.LateCancel ? " (late)" : string.Empty) : null
                };
                AppointmentDetails.DataSource = new[] { row };
                AppointmentDetails.DataBind();
                LetterLink.NavigateUrl = "~/Handlers/Letter.ashx?id=" + a.AppointmentId;
                FindTimesLink.NavigateUrl = "~/Schedule/Book.aspx?patientId=" + a.PatientId;
                ActionsPanel.Visible = AppointmentRules.IsActive(status) && status != AppointmentStatus.InRoom;

                Encounter encounter = a.Encounters.FirstOrDefault();
                if (encounter != null)
                {
                    EncounterLink.Visible = true;
                    EncounterLink.NavigateUrl = "~/Clinical/Encounter.aspx?id=" + encounter.EncounterId;
                }

                if (status == AppointmentStatus.Cancelled || status == AppointmentStatus.NoShow)
                {
                    WaitlistGrid.DataSource = new AppointmentRepository().WaitlistCandidates(a.ProviderId, a.SiteId, a.StartsAt.Date, a.AppointmentTypeId);
                    WaitlistGrid.DataBind();
                }
                else
                {
                    WaitlistGrid.Visible = false;
                }
            }
        }
    }
}
