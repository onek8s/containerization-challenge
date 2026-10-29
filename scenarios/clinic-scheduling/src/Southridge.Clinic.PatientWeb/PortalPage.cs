using System;
using System.Web.UI;
using Southridge.Clinic.PatientWeb.BookingWs;

namespace Southridge.Clinic.PatientWeb
{
    /// <summary>Base class for the Web Forms pages of the patient site.</summary>
    public class PortalPage : Page
    {
        private IBookingClient _booking;
        private IPatientContext _patientContext;

        protected IBookingClient Booking
        {
            get { return _booking ?? (_booking = new BookingClient()); }
        }

        protected IPatientContext PatientContext
        {
            get { return _patientContext ?? (_patientContext = new SessionPatientContext(Booking, new System.Web.HttpContextWrapper(Context))); }
        }

        protected PatientSummary Patient
        {
            get { return PatientContext.Patient; }
        }

        protected void ShowMessage(string text, bool error)
        {
            ((SiteMaster)Master).ShowMessage(text, error);
        }

        /// <summary>Booking service problems are shown to the patient; anything else goes to the error page.</summary>
        protected bool TryBooking(Action action)
        {
            try
            {
                action();
                return true;
            }
            catch (BookingException ex)
            {
                ShowMessage(ex.Message, true);
                return false;
            }
        }

        protected void RequireLinkedPatient()
        {
            if (User.Identity.IsAuthenticated && Patient == null)
            {
                Response.Redirect("~/Account/Link.aspx?ReturnUrl=" + Server.UrlEncode(Request.RawUrl), true);
            }
        }
    }
}
