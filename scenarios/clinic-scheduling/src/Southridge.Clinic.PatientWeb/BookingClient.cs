using System;
using System.Collections.Generic;
using System.Net;
using System.Web.Services.Protocols;
using Southridge.Clinic.Infrastructure;
using Southridge.Clinic.PatientWeb.BookingWs;

namespace Southridge.Clinic.PatientWeb
{
    /// <summary>What the patient site needs from the booking web service (clinic-ws).</summary>
    public interface IBookingClient
    {
        SiteItem[] GetSites();
        AppointmentTypeItem[] GetAppointmentTypes();
        SlotItem[] FindSlots(int appointmentTypeId, DateTime fromDate, int days, int siteId, int providerId);
        RegistrationResponse RegisterPatient(PatientRegistration registration);
        int LinkAccount(string mrn, string lastName, DateTime dateOfBirth, string portalUserName);
        PatientSummary GetPatient(string portalUserName);
        BookingResponse Book(int patientId, int appointmentTypeId, int providerId, int siteId, DateTime start, string reason, string portalUserName);
        AppointmentItem[] GetAppointments(int patientId, DateTime fromDate, DateTime toDate);
        bool Cancel(int patientId, int appointmentId, string reason, string portalUserName);
        int SubmitIntakeForm(IntakeFormSubmission form);
    }

    /// <summary>Wraps the generated web reference: URL and API key from configuration, SOAP faults to BookingException.</summary>
    public class BookingClient : IBookingClient
    {
        private readonly string _url;
        private readonly string _apiKey;
        private readonly int _timeout;

        public BookingClient()
            : this(ClinicSettings.Required("BookingServiceUrl"), ClinicSettings.Required("BookingApiKey"),
                   int.Parse(ClinicSettings.Get("BookingTimeoutSeconds", "20"), System.Globalization.CultureInfo.InvariantCulture))
        {
        }

        public BookingClient(string url, string apiKey, int timeoutSeconds)
        {
            _url = url;
            _apiKey = apiKey;
            _timeout = timeoutSeconds * 1000;
        }

        public SiteItem[] GetSites()
        {
            return Call(s => s.GetSites());
        }

        public AppointmentTypeItem[] GetAppointmentTypes()
        {
            return Call(s => s.GetAppointmentTypes());
        }

        public SlotItem[] FindSlots(int appointmentTypeId, DateTime fromDate, int days, int siteId, int providerId)
        {
            return Call(s => s.FindSlots(appointmentTypeId, fromDate, days, siteId, providerId));
        }

        public RegistrationResponse RegisterPatient(PatientRegistration registration)
        {
            return Call(s => s.RegisterPatient(registration));
        }

        public int LinkAccount(string mrn, string lastName, DateTime dateOfBirth, string portalUserName)
        {
            return Call(s => s.LinkAccount(mrn, lastName, dateOfBirth, portalUserName));
        }

        public PatientSummary GetPatient(string portalUserName)
        {
            return Call(s => s.GetPatient(portalUserName));
        }

        public BookingResponse Book(int patientId, int appointmentTypeId, int providerId, int siteId, DateTime start, string reason, string portalUserName)
        {
            return Call(s => s.Book(patientId, appointmentTypeId, providerId, siteId, start, reason, portalUserName));
        }

        public AppointmentItem[] GetAppointments(int patientId, DateTime fromDate, DateTime toDate)
        {
            return Call(s => s.GetAppointments(patientId, fromDate, toDate));
        }

        public bool Cancel(int patientId, int appointmentId, string reason, string portalUserName)
        {
            return Call(s => s.Cancel(patientId, appointmentId, reason, portalUserName));
        }

        public int SubmitIntakeForm(IntakeFormSubmission form)
        {
            return Call(s => s.SubmitIntakeForm(form));
        }

        private T Call<T>(Func<BookingService, T> call)
        {
            using (var service = new BookingService())
            {
                service.Url = _url;
                service.Timeout = _timeout;
                service.ApiKeyHeaderValue = new ApiKeyHeader { ApiKey = _apiKey };
                try
                {
                    return call(service);
                }
                catch (SoapException ex)
                {
                    string code = ex.Detail != null && ex.Detail.FirstChild != null ? ex.Detail.FirstChild.InnerText : null;
                    ClinicLog.Warning(LogCategory.General, "Booking service fault " + (code ?? ex.Code.Name) + ": " + ex.Message);
                    throw new BookingException(code, ex.Message, ex);
                }
                catch (WebException ex)
                {
                    ClinicLog.Error(LogCategory.General, "Booking service unavailable at " + _url, ex);
                    throw new BookingException("UNAVAILABLE", "Online booking is not available right now. Please call the clinic.", ex);
                }
            }
        }
    }

    [Serializable]
    public class BookingException : Exception
    {
        public BookingException(string code, string message, Exception inner)
            : base(message, inner)
        {
            Code = code;
        }

        protected BookingException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
            : base(info, context)
        {
        }

        public string Code { get; private set; }

        public bool IsRuleViolation
        {
            get { return Code == "RULE"; }
        }
    }
}
