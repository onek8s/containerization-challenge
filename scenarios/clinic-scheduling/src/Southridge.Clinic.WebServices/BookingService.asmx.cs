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
    /// Booking operations for the patient web site. The site authenticates the patient (forms authentication) and
    /// calls these operations with its own API key, passing the linked patient id.
    /// </summary>
    [WebService(Namespace = ServiceNamespaces.Booking, Description = "Southridge patient booking service")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [ToolboxItem(false)]
    public class BookingService : ClinicWebServiceBase
    {
        private const string ClientType = "Portal";

        public ApiKeyHeader Credentials;

        [WebMethod(Description = "Sites that take online bookings.", CacheDuration = 300)]
        [SoapHeader("Credentials")]
        public SiteItem[] GetSites()
        {
            Authenticate(Credentials, ClientType);
            return ClinicLookups.Sites().Where(s => s.OnlineBooking)
                .Select(s => new SiteItem { SiteId = s.SiteId, Code = s.Code, Name = s.Name, Address = s.Address1 + ", " + s.City, Phone = PhoneNumber.Format(s.Phone) })
                .ToArray();
        }

        [WebMethod(Description = "Appointment types patients can book themselves.")]
        [SoapHeader("Credentials")]
        public AppointmentTypeItem[] GetAppointmentTypes()
        {
            Authenticate(Credentials, ClientType);
            return ClinicLookups.AppointmentTypes(true)
                .Select(t => new AppointmentTypeItem { AppointmentTypeId = t.AppointmentTypeId, Name = t.Name, DurationMinutes = t.DurationMinutes, NewPatient = t.NewPatient })
                .ToArray();
        }

        [WebMethod(Description = "Open slots for an appointment type.")]
        [SoapHeader("Credentials")]
        public SlotItem[] FindSlots(int appointmentTypeId, DateTime fromDate, int days, int siteId, int providerId)
        {
            Authenticate(Credentials, ClientType);
            return Execute("FindSlots", delegate
            {
                IList<AvailableSlot> slots = new AvailabilityService().FindSlots(new SlotQuery
                {
                    AppointmentTypeId = appointmentTypeId,
                    FromDate = fromDate < DateTime.Today ? DateTime.Today : fromDate,
                    Days = Math.Min(Math.Max(days, 1), 28),
                    SiteId = siteId > 0 ? siteId : (int?)null,
                    ProviderId = providerId > 0 ? providerId : (int?)null,
                    OnlineOnly = true,
                    MaxResults = ClinicSettings.GetInt("OnlineMaxSlots", 60)
                }, DateTime.Now);
                return slots.Select(s => new SlotItem { Start = s.Start, End = s.End, ProviderId = s.ProviderId, ProviderName = s.ProviderName, SiteId = s.SiteId, SiteName = s.SiteName }).ToArray();
            });
        }

        [WebMethod(Description = "Registers a new patient from the online form.")]
        [SoapHeader("Credentials")]
        public RegistrationResponse RegisterPatient(PatientRegistration registration)
        {
            Authenticate(Credentials, ClientType);
            return Execute("RegisterPatient", delegate
            {
                var patient = new NewPatient
                {
                    FirstName = registration.FirstName,
                    LastName = registration.LastName,
                    DateOfBirth = registration.DateOfBirth,
                    Sex = (byte)ParseSex(registration.Sex),
                    Address1 = registration.Address1,
                    City = registration.City,
                    State = registration.State,
                    Zip = registration.Zip,
                    MobilePhone = registration.MobilePhone,
                    Email = registration.Email,
                    PreferredSiteId = registration.PreferredSiteId,
                    PortalUserName = registration.PortalUserName
                };
                RegistrationResult result = new PatientService().Register(patient, "portal:" + registration.PortalUserName, false);
                if (result.PossibleDuplicate)
                {
                    return new RegistrationResponse
                    {
                        Success = false,
                        Message = "We may already have a record for you. Please link your account with your medical record number or call the clinic."
                    };
                }

                return new RegistrationResponse { Success = true, PatientId = result.PatientId, Mrn = result.Mrn };
            });
        }

        [WebMethod(Description = "Links an online account to an existing patient record. Returns 0 when nothing matches.")]
        [SoapHeader("Credentials")]
        public int LinkAccount(string mrn, string lastName, DateTime dateOfBirth, string portalUserName)
        {
            Authenticate(Credentials, ClientType);
            return Execute("LinkAccount", delegate
            {
                string normalized = MedicalRecordNumber.Normalize(mrn);
                if (normalized == null)
                {
                    return 0;
                }

                int? id = new PatientRepository().LinkPortalAccount(normalized, lastName, dateOfBirth, portalUserName);
                ClinicLog.Audit("portal:" + portalUserName, "LinkAccount", id.HasValue ? "Linked to " + normalized : "No match for " + normalized);
                return id ?? 0;
            });
        }

        [WebMethod(Description = "Patient linked to an online account.")]
        [SoapHeader("Credentials")]
        public PatientSummary GetPatient(string portalUserName)
        {
            Authenticate(Credentials, ClientType);
            return Execute("GetPatient", delegate
            {
                using (ClinicDataContext db = ClinicDataContextFactory.CreateReadOnly())
                {
                    Patient p = db.Patients.SingleOrDefault(x => x.PortalUserName == portalUserName && x.IsActive);
                    return p == null ? null : new PatientSummary
                    {
                        PatientId = p.PatientId,
                        Mrn = p.Mrn,
                        FirstName = p.FirstName,
                        LastName = p.LastName,
                        DateOfBirth = p.DateOfBirth,
                        Email = p.Email,
                        MobilePhone = p.MobilePhone,
                        Balance = p.Balance
                    };
                }
            });
        }

        [WebMethod(Description = "Books an online appointment.")]
        [SoapHeader("Credentials")]
        public BookingResponse Book(int patientId, int appointmentTypeId, int providerId, int siteId, DateTime start, string reason, string portalUserName)
        {
            Authenticate(Credentials, ClientType);
            try
            {
                BookingResult result = new SchedulingService().Book(new BookingRequest
                {
                    PatientId = patientId,
                    AppointmentTypeId = appointmentTypeId,
                    ProviderId = providerId,
                    SiteId = siteId,
                    StartsAt = start,
                    Reason = reason,
                    Source = AppointmentSource.Online,
                    BookedBy = "portal:" + portalUserName
                });
                return new BookingResponse { Success = true, AppointmentId = result.AppointmentId, ConfirmationCode = result.ConfirmationCode };
            }
            catch (ClinicRuleException ex)
            {
                return new BookingResponse { Success = false, Message = ex.Message };
            }
        }

        [WebMethod(Description = "Upcoming and recent appointments for a patient.")]
        [SoapHeader("Credentials")]
        public AppointmentItem[] GetAppointments(int patientId, DateTime fromDate, DateTime toDate)
        {
            Authenticate(Credentials, ClientType);
            return Execute("GetAppointments", delegate
            {
                DateTime now = DateTime.Now;
                return ClinicLookups.PatientAppointments(patientId, fromDate, toDate)
                    .Select(a => new AppointmentItem
                    {
                        AppointmentId = a.AppointmentId,
                        ConfirmationCode = a.ConfirmationCode,
                        Start = a.StartsAt,
                        Status = a.StatusText,
                        AppointmentType = a.AppointmentType,
                        ProviderName = a.ProviderName,
                        SiteName = a.SiteName,
                        SiteAddress = a.SiteAddress,
                        SitePhone = a.SitePhone,
                        CanCancel = AppointmentRules.IsActive(a.Status) && a.Status != AppointmentStatus.CheckedIn && a.StartsAt > now.AddHours(ClinicSettings.GetInt("OnlineCancelMinHours", 2))
                    }).ToArray();
            });
        }

        [WebMethod(Description = "Cancels an appointment on behalf of the patient.")]
        [SoapHeader("Credentials")]
        public bool Cancel(int patientId, int appointmentId, string reason, string portalUserName)
        {
            Authenticate(Credentials, ClientType);
            return Execute("Cancel", delegate
            {
                PatientAppointment appointment = ClinicLookups.PatientAppointments(patientId, DateTime.Today, DateTime.Today.AddYears(2))
                    .FirstOrDefault(a => a.AppointmentId == appointmentId);
                if (appointment == null)
                {
                    throw new ClinicRuleException("The appointment was not found.");
                }

                new SchedulingService().Cancel(appointmentId, string.IsNullOrEmpty(reason) ? "Cancelled online" : reason, "portal:" + portalUserName);
                return true;
            });
        }

        [WebMethod(Description = "Saves the pre-visit intake form.")]
        [SoapHeader("Credentials")]
        public int SubmitIntakeForm(IntakeFormSubmission form)
        {
            Authenticate(Credentials, ClientType);
            return Execute("SubmitIntakeForm", delegate
            {
                using (ClinicDataContext db = ClinicDataContextFactory.Create())
                {
                    var entity = new IntakeForm
                    {
                        PatientId = form.PatientId,
                        AppointmentId = form.AppointmentId,
                        Medications = Trim(form.Medications, 2000),
                        Allergies = Trim(form.Allergies, 1000),
                        Conditions = Trim(form.Conditions, 2000),
                        ReasonForVisit = Trim(form.ReasonForVisit, 1000),
                        PharmacyName = Trim(form.PharmacyName, 100),
                        PharmacyPhone = PhoneNumber.Digits(form.PharmacyPhone),
                        SubmittedOn = DateTime.Now
                    };
                    db.IntakeForms.InsertOnSubmit(entity);
                    db.SubmitChanges();
                    return entity.IntakeFormId;
                }
            });
        }

        private static Sex ParseSex(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return Sex.Unknown;
            }

            switch (char.ToUpperInvariant(value[0]))
            {
                case 'F': return Sex.Female;
                case 'M': return Sex.Male;
                default: return Sex.Unknown;
            }
        }

        private static string Trim(string value, int max)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            value = value.Trim();
            return value.Length > max ? value.Substring(0, max) : value;
        }
    }
}
