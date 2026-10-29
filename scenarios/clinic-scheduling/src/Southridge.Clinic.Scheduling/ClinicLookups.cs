using System;
using System.Collections.Generic;
using System.Linq;
using Southridge.Clinic.Data;
using Southridge.Clinic.Domain;

namespace Southridge.Clinic.Scheduling
{
    [Serializable]
    public class PatientAppointment
    {
        public int AppointmentId { get; set; }
        public string ConfirmationCode { get; set; }
        public DateTime StartsAt { get; set; }
        public DateTime EndsAt { get; set; }
        public AppointmentStatus Status { get; set; }
        public string AppointmentType { get; set; }
        public string ProviderName { get; set; }
        public int SiteId { get; set; }
        public string SiteName { get; set; }
        public string SiteAddress { get; set; }
        public string SitePhone { get; set; }

        public string StatusText
        {
            get { return ClinicLookups.StatusText(Status); }
        }
    }

    /// <summary>Reference data and patient appointment lists shared by the web services, WCF services and both sites.</summary>
    public static class ClinicLookups
    {
        private static readonly object Sync = new object();
        private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(10);
        private static List<Site> _sites;
        private static List<AppointmentType> _types;
        private static List<Provider> _providers;
        private static DateTime _loadedAt = DateTime.MinValue;

        public static IList<Site> Sites()
        {
            EnsureLoaded();
            return _sites;
        }

        public static IList<AppointmentType> AppointmentTypes(bool onlineOnly)
        {
            EnsureLoaded();
            return _types.Where(t => !onlineOnly || t.AllowOnline).ToList();
        }

        public static IList<Provider> Providers(int? siteId, string specialtyCode)
        {
            EnsureLoaded();
            return _providers
                .Where(p => (!siteId.HasValue || p.DefaultSiteId == siteId.Value) && (string.IsNullOrEmpty(specialtyCode) || p.SpecialtyCode == specialtyCode))
                .ToList();
        }

        public static Site Site(int siteId)
        {
            return Sites().FirstOrDefault(s => s.SiteId == siteId);
        }

        public static Site SiteByCode(string code)
        {
            return Sites().FirstOrDefault(s => string.Equals(s.Code, code, StringComparison.OrdinalIgnoreCase));
        }

        public static AppointmentType AppointmentType(int appointmentTypeId)
        {
            EnsureLoaded();
            return _types.FirstOrDefault(t => t.AppointmentTypeId == appointmentTypeId);
        }

        public static string ProviderName(Provider p)
        {
            return p.FirstName + " " + p.LastName + (string.IsNullOrEmpty(p.Credentials) ? string.Empty : ", " + p.Credentials);
        }

        public static void Invalidate()
        {
            lock (Sync)
            {
                _loadedAt = DateTime.MinValue;
            }
        }

        public static Patient PatientByMrn(string mrn)
        {
            string normalized = MedicalRecordNumber.Normalize(mrn);
            if (normalized == null)
            {
                return null;
            }

            using (ClinicDataContext db = ClinicDataContextFactory.CreateReadOnly())
            {
                return db.Patients.SingleOrDefault(p => p.Mrn == normalized && p.IsActive);
            }
        }

        public static List<PatientAppointment> PatientAppointments(int patientId, DateTime fromDate, DateTime toDate)
        {
            using (ClinicDataContext db = ClinicDataContextFactory.CreateReadOnly())
            {
                return (from a in db.Appointments
                        where a.PatientId == patientId && a.StartsAt >= fromDate && a.StartsAt < toDate && a.Status != (byte)AppointmentStatus.Rescheduled
                        orderby a.StartsAt
                        select new
                        {
                            a.AppointmentId,
                            a.ConfirmationCode,
                            a.StartsAt,
                            a.EndsAt,
                            a.Status,
                            Type = a.AppointmentType.Name,
                            a.Provider.FirstName,
                            a.Provider.LastName,
                            a.Provider.Credentials,
                            a.SiteId,
                            SiteName = a.Site.Name,
                            a.Site.Address1,
                            a.Site.City,
                            a.Site.Phone
                        }).ToList()
                       .Select(a => new PatientAppointment
                       {
                           AppointmentId = a.AppointmentId,
                           ConfirmationCode = a.ConfirmationCode,
                           StartsAt = a.StartsAt,
                           EndsAt = a.EndsAt,
                           Status = (AppointmentStatus)a.Status,
                           AppointmentType = a.Type,
                           ProviderName = a.FirstName + " " + a.LastName + (a.Credentials == null ? string.Empty : ", " + a.Credentials),
                           SiteId = a.SiteId,
                           SiteName = a.SiteName,
                           SiteAddress = a.Address1 + ", " + a.City,
                           SitePhone = PhoneNumber.Format(a.Phone)
                       }).ToList();
            }
        }

        public static PatientAppointment AppointmentByConfirmation(string confirmationCode)
        {
            if (string.IsNullOrEmpty(confirmationCode))
            {
                return null;
            }

            string code = confirmationCode.Trim().ToUpperInvariant();
            int patientId;
            DateTime startsAt;
            using (ClinicDataContext db = ClinicDataContextFactory.CreateReadOnly())
            {
                var hit = db.Appointments.Where(a => a.ConfirmationCode == code).Select(a => new { a.PatientId, a.StartsAt }).FirstOrDefault();
                if (hit == null)
                {
                    return null;
                }

                patientId = hit.PatientId;
                startsAt = hit.StartsAt;
            }

            return PatientAppointments(patientId, startsAt.Date, startsAt.Date.AddDays(1)).FirstOrDefault(a => a.ConfirmationCode == code);
        }

        public static string StatusText(AppointmentStatus status)
        {
            switch (status)
            {
                case AppointmentStatus.CheckedIn: return "Checked in";
                case AppointmentStatus.InRoom: return "In room";
                case AppointmentStatus.NoShow: return "No show";
                default: return status.ToString();
            }
        }

        private static void EnsureLoaded()
        {
            if (DateTime.UtcNow - _loadedAt < CacheFor)
            {
                return;
            }

            lock (Sync)
            {
                if (DateTime.UtcNow - _loadedAt < CacheFor)
                {
                    return;
                }

                using (ClinicDataContext db = ClinicDataContextFactory.CreateReadOnly())
                {
                    _sites = db.Sites.Where(s => s.IsActive).OrderBy(s => s.Name).ToList();
                    _types = db.AppointmentTypes.Where(t => t.IsActive).OrderBy(t => t.Name).ToList();
                    _providers = db.Providers.Where(p => p.IsActive).OrderBy(p => p.LastName).ThenBy(p => p.FirstName).ToList();
                }

                _loadedAt = DateTime.UtcNow;
            }
        }
    }
}
