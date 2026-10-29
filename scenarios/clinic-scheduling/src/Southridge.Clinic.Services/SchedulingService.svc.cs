using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Activation;
using Southridge.Clinic.Contracts;
using Southridge.Clinic.Data;
using Southridge.Clinic.Data.Repositories;
using Southridge.Clinic.Domain;
using Southridge.Clinic.Referrals;
using Southridge.Clinic.Scheduling;
using SchedulingEngine = Southridge.Clinic.Scheduling.SchedulingService;

namespace Southridge.Clinic.Services
{
    [ServiceBehavior(Namespace = Namespaces.Scheduling, InstanceContextMode = InstanceContextMode.PerCall, ConcurrencyMode = ConcurrencyMode.Multiple)]
    [AspNetCompatibilityRequirements(RequirementsMode = AspNetCompatibilityRequirementsMode.Allowed)]
    public class SchedulingService : ISchedulingService
    {
        public List<SiteInfo> GetSites()
        {
            return ServiceCall.Run("GetSites", () => ClinicLookups.Sites().Select(s => ToInfo(s)).ToList());
        }

        public List<AppointmentTypeInfo> GetAppointmentTypes(bool onlineOnly)
        {
            return ServiceCall.Run("GetAppointmentTypes", () => ClinicLookups.AppointmentTypes(onlineOnly)
                .Select(t => new AppointmentTypeInfo
                {
                    AppointmentTypeId = t.AppointmentTypeId,
                    Code = t.Code,
                    Name = t.Name,
                    DurationMinutes = t.DurationMinutes,
                    RequiresReferral = t.Code == "CARDNP" || t.Code == "ORTHNP"
                }).ToList());
        }

        public List<ProviderInfo> GetProviders(int? siteId, string specialtyCode)
        {
            return ServiceCall.Run("GetProviders", () => ClinicLookups.Providers(siteId, specialtyCode)
                .Select(p => new ProviderInfo
                {
                    ProviderId = p.ProviderId,
                    Name = p.FirstName + " " + p.LastName,
                    Credentials = p.Credentials,
                    SpecialtyCode = p.SpecialtyCode,
                    Npi = p.Npi,
                    AcceptingNewPatients = p.AcceptsNewPatients
                }).ToList());
        }

        public List<SlotInfo> FindSlots(SlotSearch search)
        {
            return ServiceCall.Run("FindSlots", delegate
            {
                if (search == null)
                {
                    throw new ClinicRuleException("A search is required.");
                }

                return new AvailabilityService().FindSlots(new SlotQuery
                {
                    AppointmentTypeId = search.AppointmentTypeId,
                    FromDate = search.FromDate,
                    Days = Math.Min(Math.Max(search.Days, 1), 31),
                    SiteId = search.SiteId,
                    ProviderId = search.ProviderId,
                    MaxResults = search.MaxResults > 0 ? Math.Min(search.MaxResults, 200) : 50
                }, DateTime.Now).Select(s => ToInfo(s)).ToList();
            });
        }

        public BookingConfirmation Book(BookAppointmentRequest request)
        {
            return ServiceCall.Run("Book", delegate
            {
                Patient patient = ClinicLookups.PatientByMrn(request.Mrn);
                if (patient == null)
                {
                    throw new NotFoundException("Patient " + request.Mrn + " was not found.");
                }

                int? referralId = null;
                if (!string.IsNullOrEmpty(request.ReferralNumber))
                {
                    referralId = ReferralSessions.InUnitOfWork(session =>
                    {
                        Referral referral = new ReferralRepository(session).GetByNumber(request.ReferralNumber);
                        return referral == null ? (int?)null : referral.Id;
                    });
                }

                BookingResult result = new SchedulingEngine().Book(new BookingRequest
                {
                    PatientId = patient.PatientId,
                    ProviderId = request.ProviderId,
                    SiteId = request.SiteId,
                    AppointmentTypeId = request.AppointmentTypeId,
                    StartsAt = request.Start,
                    Reason = request.Reason,
                    ReferralId = referralId,
                    Source = AppointmentSource.Interface,
                    BookedBy = "svc:" + (string.IsNullOrEmpty(request.BookedBy) ? "partner" : request.BookedBy)
                });

                if (referralId.HasValue)
                {
                    ReferralWorkflowService.LinkAppointment(referralId.Value, result.AppointmentId, "svc:" + request.BookedBy);
                }

                PatientAppointment booked = ClinicLookups.AppointmentByConfirmation(result.ConfirmationCode);
                return new BookingConfirmation
                {
                    AppointmentId = result.AppointmentId,
                    ConfirmationCode = result.ConfirmationCode,
                    Start = booked.StartsAt,
                    ProviderName = booked.ProviderName,
                    SiteName = booked.SiteName
                };
            });
        }

        public void Cancel(string confirmationCode, string mrn, string reason)
        {
            ServiceCall.Run("Cancel", delegate
            {
                Patient patient = ClinicLookups.PatientByMrn(mrn);
                PatientAppointment appointment = ClinicLookups.AppointmentByConfirmation(confirmationCode);
                if (patient == null || appointment == null
                    || !ClinicLookups.PatientAppointments(patient.PatientId, appointment.StartsAt.Date, appointment.StartsAt.Date.AddDays(1)).Any(a => a.AppointmentId == appointment.AppointmentId))
                {
                    throw new NotFoundException("Appointment " + confirmationCode + " was not found for " + mrn + ".");
                }

                new SchedulingEngine().Cancel(appointment.AppointmentId, string.IsNullOrEmpty(reason) ? "Cancelled by partner" : reason, "svc:partner");
            });
        }

        public List<AppointmentInfo> GetAppointments(string mrn, DateTime from, DateTime to)
        {
            return ServiceCall.Run("GetAppointments", delegate
            {
                Patient patient = ClinicLookups.PatientByMrn(mrn);
                if (patient == null)
                {
                    throw new NotFoundException("Patient " + mrn + " was not found.");
                }

                return ClinicLookups.PatientAppointments(patient.PatientId, from, to).Select(a => ToInfo(a)).ToList();
            });
        }

        internal static SiteInfo ToInfo(Site s)
        {
            return new SiteInfo { SiteId = s.SiteId, Code = s.Code, Name = s.Name, Address = s.Address1, City = s.City, Phone = PhoneNumber.Format(s.Phone), OnlineBooking = s.OnlineBooking };
        }

        internal static SlotInfo ToInfo(AvailableSlot s)
        {
            return new SlotInfo { Start = s.Start, End = s.End, ProviderId = s.ProviderId, ProviderName = s.ProviderName, SiteId = s.SiteId, SiteName = s.SiteName };
        }

        internal static AppointmentInfo ToInfo(PatientAppointment a)
        {
            return new AppointmentInfo
            {
                AppointmentId = a.AppointmentId,
                ConfirmationCode = a.ConfirmationCode,
                Start = a.StartsAt,
                End = a.EndsAt,
                Status = a.StatusText,
                AppointmentType = a.AppointmentType,
                ProviderName = a.ProviderName,
                SiteName = a.SiteName,
                CanCancelOnline = AppointmentRules.IsActive(a.Status) && a.StartsAt > DateTime.Now.AddHours(2)
            };
        }
    }
}
