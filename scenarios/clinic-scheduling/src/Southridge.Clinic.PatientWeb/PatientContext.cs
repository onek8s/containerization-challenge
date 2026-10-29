using System.Web;
using Southridge.Clinic.PatientWeb.BookingWs;

namespace Southridge.Clinic.PatientWeb
{
    /// <summary>The patient record linked to the signed-in online account.</summary>
    public interface IPatientContext
    {
        string UserName { get; }

        /// <summary>Null when the account has not been linked to a patient record yet.</summary>
        PatientSummary Patient { get; }

        void Refresh();
    }

    /// <summary>Looks the patient up once per session through the booking service and keeps it in session state.</summary>
    public class SessionPatientContext : IPatientContext
    {
        private const string Key = "Portal.Patient";
        private readonly IBookingClient _booking;
        private readonly HttpContextBase _context;

        public SessionPatientContext(IBookingClient booking, HttpContextBase context)
        {
            _booking = booking;
            _context = context;
        }

        public string UserName
        {
            get { return _context.User.Identity.Name; }
        }

        public PatientSummary Patient
        {
            get
            {
                if (!_context.User.Identity.IsAuthenticated)
                {
                    return null;
                }

                var patient = _context.Session[Key] as PatientSummary;
                if (patient == null)
                {
                    patient = _booking.GetPatient(UserName);
                    if (patient != null)
                    {
                        _context.Session[Key] = patient;
                    }
                }

                return patient;
            }
        }

        public void Refresh()
        {
            _context.Session.Remove(Key);
        }

        /// <summary>For Web Forms pages, which are not built by Ninject.</summary>
        public static IPatientContext ForCurrentRequest()
        {
            return new SessionPatientContext(new BookingClient(), new HttpContextWrapper(HttpContext.Current));
        }
    }
}
