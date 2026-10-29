using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Web.Services;
using System.Web.Services.Protocols;
using Southridge.Clinic.Data;
using Southridge.Clinic.Data.Repositories;
using Southridge.Clinic.Domain;
using Southridge.Clinic.Infrastructure;
using Southridge.Clinic.Scheduling;

namespace Southridge.Clinic.WebServices
{
    /// <summary>
    /// Self check-in kiosks in the waiting rooms. Each kiosk has its own API key, which is tied to a site.
    /// </summary>
    [WebService(Namespace = ServiceNamespaces.Kiosk, Description = "Southridge check-in kiosk service")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [ToolboxItem(false)]
    public class KioskService : ClinicWebServiceBase
    {
        private const string ClientType = "Kiosk";

        public ApiKeyHeader Credentials;

        [WebMethod(Description = "Kiosk heartbeat; returns the site name and any notice to show.")]
        [SoapHeader("Credentials")]
        public KioskStatus Ping()
        {
            ApiClientInfo kiosk = Authenticate(Credentials, ClientType);
            Site site = ClinicLookups.Site(kiosk.SiteId.Value);
            using (ClinicDataContext db = ClinicDataContextFactory.CreateReadOnly())
            {
                DateTime today = DateTime.Today;
                return new KioskStatus
                {
                    SiteName = site.Name,
                    ServerTime = DateTime.Now,
                    CheckedInToday = db.Appointments.Count(a => a.SiteId == site.SiteId && a.StartsAt >= today && a.StartsAt < today.AddDays(1) && a.CheckedInOn != null),
                    Notice = new SystemRepository().GetSetting("KioskNotice." + site.Code, string.Empty)
                };
            }
        }

        [WebMethod(Description = "Finds today's appointments by last name and date of birth, or by confirmation code.")]
        [SoapHeader("Credentials")]
        public KioskAppointment[] FindAppointments(string lastName, DateTime? dateOfBirth, string confirmationCode)
        {
            ApiClientInfo kiosk = Authenticate(Credentials, ClientType);
            return Execute("FindAppointments", delegate
            {
                if (string.IsNullOrEmpty(confirmationCode) && (string.IsNullOrEmpty(lastName) || !dateOfBirth.HasValue))
                {
                    throw new ClinicRuleException("Enter your last name and date of birth, or your confirmation code.");
                }

                DateTime now = DateTime.Now;
                List<CheckInMatch> matches = new AppointmentRepository().FindForCheckIn(kiosk.SiteId.Value, lastName, dateOfBirth, confirmationCode, now);
                return matches.Select(m => new KioskAppointment
                {
                    AppointmentId = m.AppointmentId,
                    Start = m.StartsAt,
                    FirstName = m.FirstName,
                    LastInitial = string.IsNullOrEmpty(m.LastName) ? string.Empty : m.LastName.Substring(0, 1) + ".",
                    ProviderName = m.ProviderName,
                    Copay = m.Copay,
                    HasBalance = m.Balance > 0,
                    IntakeFormDue = m.IntakeFormDue,
                    CanCheckIn = (m.Status == (byte)AppointmentStatus.Scheduled || m.Status == (byte)AppointmentStatus.Confirmed)
                                 && AppointmentRules.IsKioskCheckInWindow(m.StartsAt, now)
                }).ToArray();
            });
        }

        [WebMethod(Description = "Checks the patient in. Patients with a balance, a due intake form or outside the window are sent to the desk.")]
        [SoapHeader("Credentials")]
        public KioskCheckInResponse CheckIn(int appointmentId, string lastName, DateTime dateOfBirth)
        {
            ApiClientInfo kiosk = Authenticate(Credentials, ClientType);
            return Execute("CheckIn", delegate
            {
                DateTime now = DateTime.Now;
                CheckInMatch match = new AppointmentRepository().FindForCheckIn(kiosk.SiteId.Value, lastName, dateOfBirth, null, now)
                    .FirstOrDefault(m => m.AppointmentId == appointmentId);
                if (match == null)
                {
                    return new KioskCheckInResponse { SeeFrontDesk = true, Message = "We could not find your appointment. Please see the front desk." };
                }

                if (!AppointmentRules.IsKioskCheckInWindow(match.StartsAt, now))
                {
                    return new KioskCheckInResponse { SeeFrontDesk = true, Message = "Your appointment is at " + match.StartsAt.ToString("h:mm tt") + ". Please see the front desk." };
                }

                decimal threshold = ClinicSettings.GetDecimal("KioskBalanceLimit", 100m);
                if (match.Balance > threshold || match.IntakeFormDue)
                {
                    return new KioskCheckInResponse { SeeFrontDesk = true, Message = "Please see the front desk to complete your check-in." };
                }

                new SchedulingService().CheckIn(appointmentId, null, "kiosk:" + kiosk.Name);
                string message = "You are checked in for " + match.StartsAt.ToString("h:mm tt") + " with " + match.ProviderName + ". Please have a seat.";
                if (match.Copay > 0)
                {
                    message += " Your copay of " + match.Copay.ToString("C") + " will be collected at the desk.";
                }

                return new KioskCheckInResponse { CheckedIn = true, Message = message };
            });
        }
    }
}
