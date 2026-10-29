using System;
using System.Collections.Generic;
using System.Linq;
using Southridge.Clinic.Data;
using Southridge.Clinic.Data.Repositories;
using Southridge.Clinic.Domain;
using Southridge.Clinic.Hl7;
using Southridge.Clinic.Infrastructure;

namespace Southridge.Clinic.Scheduling
{
    /// <summary>
    /// Booking, cancellation, rescheduling and front desk status changes. Used by the staff site, the ASMX
    /// booking and kiosk services and the WCF scheduling service so the rules are applied the same way.
    /// </summary>
    public class SchedulingService
    {
        private readonly AppointmentRepository _appointments;
        private readonly InterfaceNotifier _notifier;
        private readonly IClock _clock;

        public SchedulingService()
            : this(new AppointmentRepository(), new InterfaceNotifier(), new SystemClock())
        {
        }

        public SchedulingService(AppointmentRepository appointments, InterfaceNotifier notifier, IClock clock)
        {
            _appointments = appointments;
            _notifier = notifier;
            _clock = clock;
        }

        public BookingResult Book(BookingRequest request)
        {
            if (request.StartsAt < _clock.Now.AddMinutes(-5) && request.Source != AppointmentSource.Staff)
            {
                throw new ClinicRuleException("The requested time has already passed.");
            }

            if (request.Source == AppointmentSource.Online && !AppointmentRules.CanBookOnline(request.StartsAt, _clock.Now))
            {
                throw new ClinicRuleException("Online bookings must be at least " + AppointmentRules.OnlineBookingMinLeadHours + " hours ahead.");
            }

            BookingResult result = _appointments.Book(request);
            ClinicLog.Info(LogCategory.Scheduling, "Appointment {0} booked for patient {1} with provider {2} at {3:g} by {4}",
                result.AppointmentId, request.PatientId, request.ProviderId, request.StartsAt, request.BookedBy);
            Notify(result.AppointmentId, request.RescheduledFromId.HasValue ? "S13" : "S12");
            return result;
        }

        public BookingResult Reschedule(int appointmentId, DateTime newStart, int? providerId, int? siteId, string userName, AppointmentSource source)
        {
            using (ClinicDataContext db = ClinicDataContextFactory.CreateReadOnly())
            {
                Appointment current = db.Appointments.Single(a => a.AppointmentId == appointmentId);
                if (!AppointmentRules.IsActive((AppointmentStatus)current.Status))
                {
                    throw new ClinicRuleException("Only scheduled appointments can be rescheduled.");
                }

                return Book(new BookingRequest
                {
                    PatientId = current.PatientId,
                    ProviderId = providerId ?? current.ProviderId,
                    SiteId = siteId ?? current.SiteId,
                    AppointmentTypeId = current.AppointmentTypeId,
                    StartsAt = newStart,
                    DurationMinutes = (int)(current.EndsAt - current.StartsAt).TotalMinutes,
                    Source = source,
                    Reason = current.Reason,
                    ReferralId = current.ReferralId,
                    RescheduledFromId = appointmentId,
                    BookedBy = userName
                });
            }
        }

        public void Cancel(int appointmentId, string reason, string userName)
        {
            ChangeStatus(appointmentId, AppointmentStatus.Cancelled, reason, userName, null);
            Notify(appointmentId, "S15");
        }

        public void CheckIn(int appointmentId, decimal? copayCollected, string userName)
        {
            ChangeStatus(appointmentId, AppointmentStatus.CheckedIn, null, userName, copayCollected);
        }

        public void ChangeStatus(int appointmentId, AppointmentStatus status, string reason, string userName, decimal? copayCollected)
        {
            using (ClinicDataContext db = ClinicDataContextFactory.CreateReadOnly())
            {
                Appointment current = db.Appointments.SingleOrDefault(a => a.AppointmentId == appointmentId);
                if (current == null)
                {
                    throw new ClinicRuleException("Appointment " + appointmentId + " was not found.");
                }

                AppointmentRules.EnsureCanChange((AppointmentStatus)current.Status, status);
            }

            _appointments.SetStatus(appointmentId, status, userName, reason, copayCollected);
        }

        public IList<WaitlistCandidate> WaitlistFor(int providerId, int siteId, DateTime date, int? appointmentTypeId)
        {
            return _appointments.WaitlistCandidates(providerId, siteId, date, appointmentTypeId);
        }

        public List<DayAppointment> Day(int siteId, DateTime date, int? providerId, bool includeCancelled)
        {
            return _appointments.ListByDay(siteId, date, providerId, includeCancelled);
        }

        private void Notify(int appointmentId, string trigger)
        {
            try
            {
                _notifier.AppointmentEvent(appointmentId, trigger);
            }
            catch (Exception ex)
            {
                // The booking stands; a missing SIU is resent from the interface monitor.
                ClinicLog.Error(LogCategory.Interface, "Could not queue SIU^" + trigger + " for appointment " + appointmentId, ex);
            }
        }
    }
}
