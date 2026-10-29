using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using Southridge.Clinic.Domain;

namespace Southridge.Clinic.Data.Repositories
{
    public class BookingRequest
    {
        public int PatientId { get; set; }
        public int ProviderId { get; set; }
        public int SiteId { get; set; }
        public int AppointmentTypeId { get; set; }
        public DateTime StartsAt { get; set; }
        public int? DurationMinutes { get; set; }
        public AppointmentSource Source { get; set; }
        public string Reason { get; set; }
        public bool AllowOverbook { get; set; }
        public int? ReferralId { get; set; }
        public int? RescheduledFromId { get; set; }
        public string BookedBy { get; set; }
    }

    public class BookingResult
    {
        public int AppointmentId { get; set; }
        public string ConfirmationCode { get; set; }
    }

    public class AppointmentRepository : RepositoryBase
    {
        /// <summary>Books through usp_Appointment_Book. Conflicts come back as ClinicRuleException.</summary>
        public BookingResult Book(BookingRequest request)
        {
            DbCommand cmd = Proc("dbo.usp_Appointment_Book");
            In(cmd, "@PatientId", DbType.Int32, request.PatientId);
            In(cmd, "@ProviderId", DbType.Int32, request.ProviderId);
            In(cmd, "@SiteId", DbType.Int32, request.SiteId);
            In(cmd, "@AppointmentTypeId", DbType.Int32, request.AppointmentTypeId);
            In(cmd, "@StartsAt", DbType.DateTime, request.StartsAt);
            In(cmd, "@DurationMinutes", DbType.Int32, request.DurationMinutes);
            In(cmd, "@Source", DbType.Byte, (byte)request.Source);
            In(cmd, "@Reason", DbType.String, request.Reason);
            In(cmd, "@AllowOverbook", DbType.Boolean, request.AllowOverbook);
            In(cmd, "@ReferralId", DbType.Int32, request.ReferralId);
            In(cmd, "@RescheduledFromId", DbType.Int32, request.RescheduledFromId);
            In(cmd, "@BookedBy", DbType.String, request.BookedBy);
            Out(cmd, "@AppointmentId", DbType.Int32, 4);
            Out(cmd, "@ConfirmationCode", DbType.AnsiStringFixedLength, 8);
            try
            {
                Db.ExecuteNonQuery(cmd);
            }
            catch (SqlException ex)
            {
                if (ex.Class == 16 && ex.Number == 50000)
                {
                    throw new ClinicRuleException(ex.Message, ex);
                }

                throw;
            }

            return new BookingResult
            {
                AppointmentId = OutValue<int>(cmd, "@AppointmentId"),
                ConfirmationCode = OutValue<string>(cmd, "@ConfirmationCode")
            };
        }

        public void SetStatus(int appointmentId, AppointmentStatus status, string userName, string reason, decimal? copayCollected)
        {
            DbCommand cmd = Proc("dbo.usp_Appointment_SetStatus");
            In(cmd, "@AppointmentId", DbType.Int32, appointmentId);
            In(cmd, "@Status", DbType.Byte, (byte)status);
            In(cmd, "@UserName", DbType.String, userName);
            In(cmd, "@Reason", DbType.String, reason);
            In(cmd, "@CopayCollected", DbType.Currency, copayCollected);
            Db.ExecuteNonQuery(cmd);
        }

        public List<DayAppointment> ListByDay(int siteId, DateTime date, int? providerId, bool includeCancelled)
        {
            DbCommand cmd = Proc("dbo.usp_Appointment_ListByDay");
            In(cmd, "@SiteId", DbType.Int32, siteId);
            In(cmd, "@Date", DbType.Date, date.Date);
            In(cmd, "@ProviderId", DbType.Int32, providerId);
            In(cmd, "@IncludeCancelled", DbType.Boolean, includeCancelled);
            var list = new List<DayAppointment>();
            using (IDataReader r = Db.ExecuteReader(cmd))
            {
                while (r.Read())
                {
                    list.Add(new DayAppointment
                    {
                        AppointmentId = GetInt(r, "AppointmentId"),
                        StartsAt = GetDate(r, "StartsAt"),
                        EndsAt = GetDate(r, "EndsAt"),
                        Status = Convert.ToByte(r["Status"]),
                        Source = Convert.ToByte(r["Source"]),
                        Reason = GetString(r, "Reason"),
                        IsOverbook = GetBool(r, "IsOverbook"),
                        ConfirmationCode = GetString(r, "ConfirmationCode"),
                        CheckedInOn = GetNullableDate(r, "CheckedInOn"),
                        PatientId = GetInt(r, "PatientId"),
                        Mrn = GetString(r, "Mrn"),
                        PatientName = GetString(r, "LastName") + ", " + GetString(r, "FirstName"),
                        DateOfBirth = GetDate(r, "DateOfBirth"),
                        MobilePhone = GetString(r, "MobilePhone"),
                        Balance = GetDecimal(r, "Balance"),
                        ProviderId = GetInt(r, "ProviderId"),
                        ProviderName = GetString(r, "ProviderLastName") + ", " + GetString(r, "ProviderFirstName") + " " + GetString(r, "Credentials"),
                        AppointmentTypeCode = GetString(r, "AppointmentTypeCode"),
                        AppointmentTypeName = GetString(r, "AppointmentTypeName"),
                        Color = GetString(r, "Color"),
                        PrimaryPayer = GetString(r, "PrimaryPayer"),
                        Copay = r["Copay"] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(r["Copay"]),
                        HasAlert = GetBool(r, "HasAlert")
                    });
                }
            }

            return list;
        }

        public List<CheckInMatch> FindForCheckIn(int siteId, string lastName, DateTime? dateOfBirth, string confirmationCode, DateTime today)
        {
            DbCommand cmd = Proc("dbo.usp_Appointment_FindForCheckIn");
            In(cmd, "@SiteId", DbType.Int32, siteId);
            In(cmd, "@LastName", DbType.String, lastName);
            In(cmd, "@DateOfBirth", DbType.Date, dateOfBirth);
            In(cmd, "@ConfirmationCode", DbType.AnsiStringFixedLength, string.IsNullOrEmpty(confirmationCode) ? null : confirmationCode.Trim().ToUpperInvariant());
            In(cmd, "@Today", DbType.Date, today.Date);
            var list = new List<CheckInMatch>();
            using (IDataReader r = Db.ExecuteReader(cmd))
            {
                while (r.Read())
                {
                    list.Add(new CheckInMatch
                    {
                        AppointmentId = GetInt(r, "AppointmentId"),
                        StartsAt = GetDate(r, "StartsAt"),
                        Status = Convert.ToByte(r["Status"]),
                        ConfirmationCode = GetString(r, "ConfirmationCode"),
                        PatientId = GetInt(r, "PatientId"),
                        FirstName = GetString(r, "FirstName"),
                        LastName = GetString(r, "LastName"),
                        Balance = GetDecimal(r, "Balance"),
                        ProviderName = GetString(r, "ProviderLastName") + " " + GetString(r, "Credentials"),
                        Copay = GetDecimal(r, "Copay"),
                        IntakeFormDue = GetBool(r, "IntakeFormDue")
                    });
                }
            }

            return list;
        }

        public List<ReminderCandidate> DueForReminder(DateTime from, DateTime to)
        {
            DbCommand cmd = Proc("dbo.usp_Appointment_DueForReminder");
            In(cmd, "@From", DbType.DateTime, from);
            In(cmd, "@To", DbType.DateTime, to);
            var list = new List<ReminderCandidate>();
            using (IDataReader r = Db.ExecuteReader(cmd))
            {
                while (r.Read())
                {
                    list.Add(new ReminderCandidate
                    {
                        AppointmentId = GetInt(r, "AppointmentId"),
                        StartsAt = GetDate(r, "StartsAt"),
                        ConfirmationCode = GetString(r, "ConfirmationCode"),
                        PatientId = GetInt(r, "PatientId"),
                        FirstName = GetString(r, "FirstName"),
                        LastName = GetString(r, "LastName"),
                        Email = GetString(r, "Email"),
                        MobilePhone = GetString(r, "MobilePhone"),
                        PreferredContact = GetString(r, "PreferredContact"),
                        ProviderName = GetString(r, "ProviderFirstName") + " " + GetString(r, "ProviderLastName") + ", " + GetString(r, "Credentials"),
                        SiteName = GetString(r, "SiteName"),
                        SiteAddress = GetString(r, "SiteAddress") + ", " + GetString(r, "SiteCity"),
                        SitePhone = GetString(r, "SitePhone"),
                        AppointmentTypeName = GetString(r, "AppointmentTypeName")
                    });
                }
            }

            return list;
        }

        public void ReminderSent(int appointmentId, string channel, string recipient, bool succeeded, string detail)
        {
            DbCommand cmd = Proc("dbo.usp_Appointment_ReminderSent");
            In(cmd, "@AppointmentId", DbType.Int32, appointmentId);
            In(cmd, "@Channel", DbType.AnsiString, channel);
            In(cmd, "@Recipient", DbType.String, recipient);
            In(cmd, "@Succeeded", DbType.Boolean, succeeded);
            In(cmd, "@Detail", DbType.String, detail != null && detail.Length > 400 ? detail.Substring(0, 400) : detail);
            Db.ExecuteNonQuery(cmd);
        }

        public List<NoShow> MarkNoShows(DateTime asOf, int graceMinutes)
        {
            DbCommand cmd = Proc("dbo.usp_Appointment_MarkNoShows");
            In(cmd, "@AsOf", DbType.DateTime, asOf);
            In(cmd, "@GraceMinutes", DbType.Int32, graceMinutes);
            var list = new List<NoShow>();
            using (IDataReader r = Db.ExecuteReader(cmd))
            {
                while (r.Read())
                {
                    list.Add(new NoShow
                    {
                        AppointmentId = GetInt(r, "AppointmentId"),
                        PatientId = GetInt(r, "PatientId"),
                        ProviderId = GetInt(r, "ProviderId"),
                        SiteId = GetInt(r, "SiteId"),
                        StartsAt = GetDate(r, "StartsAt")
                    });
                }
            }

            return list;
        }

        public List<WaitlistCandidate> WaitlistCandidates(int providerId, int siteId, DateTime date, int? appointmentTypeId)
        {
            DbCommand cmd = Proc("dbo.usp_Waitlist_Candidates");
            In(cmd, "@ProviderId", DbType.Int32, providerId);
            In(cmd, "@SiteId", DbType.Int32, siteId);
            In(cmd, "@Date", DbType.Date, date.Date);
            In(cmd, "@AppointmentTypeId", DbType.Int32, appointmentTypeId);
            var list = new List<WaitlistCandidate>();
            using (IDataReader r = Db.ExecuteReader(cmd))
            {
                while (r.Read())
                {
                    list.Add(new WaitlistCandidate
                    {
                        WaitlistEntryId = GetInt(r, "WaitlistEntryId"),
                        PatientId = GetInt(r, "PatientId"),
                        PatientName = GetString(r, "LastName") + ", " + GetString(r, "FirstName"),
                        MobilePhone = GetString(r, "MobilePhone"),
                        Email = GetString(r, "Email"),
                        AppointmentTypeId = GetInt(r, "AppointmentTypeId"),
                        Priority = Convert.ToByte(r["Priority"]),
                        EarliestDate = GetDate(r, "EarliestDate"),
                        LatestDate = GetNullableDate(r, "LatestDate"),
                        CreatedOn = GetDate(r, "CreatedOn")
                    });
                }
            }

            return list;
        }
    }
}
