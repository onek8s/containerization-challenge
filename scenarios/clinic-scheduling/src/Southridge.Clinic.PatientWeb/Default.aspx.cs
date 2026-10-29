using System;
using System.Linq;
using Southridge.Clinic.PatientWeb.BookingWs;

namespace Southridge.Clinic.PatientWeb
{
    public partial class DefaultPage : PortalPage
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (IsPostBack || !User.Identity.IsAuthenticated)
            {
                return;
            }

            TryBooking(delegate
            {
                PatientSummary patient = Patient;
                if (patient == null)
                {
                    return;
                }

                AppointmentItem next = Booking.GetAppointments(patient.PatientId, DateTime.Now, DateTime.Today.AddDays(180))
                    .Where(a => a.CanCancel || a.Status == "Scheduled" || a.Status == "Confirmed")
                    .OrderBy(a => a.Start)
                    .FirstOrDefault();
                if (next == null)
                {
                    NoNextPanel.Visible = true;
                    return;
                }

                NextText.Text = next.AppointmentType + " with " + next.ProviderName + ", " + next.Start.ToString("dddd MMMM d 'at' h:mm tt") + ", " + next.SiteName + ".";
                IntakeLink.NavigateUrl = "~/MyHealth/Forms/Intake?appointmentId=" + next.AppointmentId;
                NextPanel.Visible = true;
            });
        }
    }
}
