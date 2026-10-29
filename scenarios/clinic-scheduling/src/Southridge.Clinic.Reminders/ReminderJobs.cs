using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using Southridge.Clinic.Data;
using Southridge.Clinic.Data.Repositories;
using Southridge.Clinic.Domain;
using Southridge.Clinic.Infrastructure;
using Southridge.Clinic.Scheduling;

namespace Southridge.Clinic.Reminders
{
    /// <summary>Appointment reminders, no-show marking and waitlist offers (Task Scheduler on clinic-int).</summary>
    public class ReminderJobs
    {
        private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

        private readonly AppointmentRepository _appointments;
        private readonly SystemRepository _settings;
        private readonly IMailer _mailer;
        private readonly ISmsSender _sms;
        private readonly IClock _clock;

        public ReminderJobs(AppointmentRepository appointments, SystemRepository settings, IMailer mailer, ISmsSender sms, IClock clock)
        {
            _appointments = appointments;
            _settings = settings;
            _mailer = mailer;
            _sms = sms;
            _clock = clock;
        }

        /// <summary>One reminder per appointment, by text when the patient prefers it and has a mobile number, otherwise e-mail.</summary>
        public string SendReminders()
        {
            DateTime now = _clock.Now;
            int hours = _settings.GetSetting("Reminders.HoursAhead", 48);
            bool smsEnabled = _settings.GetSetting("Reminders.SmsEnabled", true);
            int sent = 0, failed = 0, skipped = 0;
            foreach (ReminderCandidate c in _appointments.DueForReminder(now, now.AddHours(hours)))
            {
                string when = c.StartsAt.ToString("dddd MMM d 'at' h:mm tt", Us);
                bool bySms = smsEnabled && c.PreferredContact == "SMS" && !string.IsNullOrEmpty(c.MobilePhone);
                string channel = bySms ? "SMS" : "Email";
                string recipient = bySms ? c.MobilePhone : c.Email;
                if (string.IsNullOrEmpty(recipient))
                {
                    _appointments.ReminderSent(c.AppointmentId, "None", string.Empty, false, "No e-mail or mobile number");
                    skipped++;
                    continue;
                }

                try
                {
                    string detail;
                    if (bySms)
                    {
                        detail = "Gateway id " + _sms.Send(c.MobilePhone, string.Format(Us,
                            "Southridge Medical Group: {0}, reminder of your {1} visit {2} at {3}. To cancel call {4}. Code {5}",
                            c.FirstName, c.AppointmentTypeName, when, c.SiteName, PhoneNumber.Format(c.SitePhone), c.ConfirmationCode));
                    }
                    else
                    {
                        _mailer.Send(c.Email, "Your appointment on " + c.StartsAt.ToString("MMMM d", Us), EmailBody(c, when), false);
                        detail = null;
                    }

                    _appointments.ReminderSent(c.AppointmentId, channel, recipient, true, detail);
                    sent++;
                }
                catch (Exception ex)
                {
                    if (!(ex is WebException || ex is SmtpException || ex is ClinicRuleException || ex is FormatException))
                    {
                        throw;
                    }

                    _appointments.ReminderSent(c.AppointmentId, channel, recipient, false, ex.Message);
                    ClinicLog.Warning(LogCategory.Scheduling, "Reminder for appointment " + c.AppointmentId + " by " + channel + " failed: " + ex.Message);
                    failed++;
                }
            }

            return sent + " sent, " + failed + " failed, " + skipped + " without contact details";
        }

        /// <summary>Marks appointments nobody checked in for; the hospital gets SIU^S26 for imaging bookings.</summary>
        public string MarkNoShows()
        {
            List<NoShow> noShows = _appointments.MarkNoShows(_clock.Now, AppointmentRules.NoShowGraceMinutes);
            var notifier = new InterfaceNotifier();
            foreach (NoShow n in noShows)
            {
                notifier.AppointmentEvent(n.AppointmentId, "S26");
            }

            return noShows.Count + " marked no-show";
        }

        /// <summary>
        /// Offers patients on the waitlist the earliest open time that fits, by text or e-mail. The patient calls to
        /// take it; an entry is offered again only after three days.
        /// </summary>
        public string OfferWaitlist()
        {
            DateTime now = _clock.Now;
            var availability = new AvailabilityService();
            int offered = 0, none = 0;
            using (ClinicDataContext db = ClinicDataContextFactory.Create())
            {
                DateTime reofferBefore = now.AddDays(-3);
                var entries = db.WaitlistEntries
                    .Where(w => w.ResolvedOn == null && (w.OfferedOn == null || w.OfferedOn < reofferBefore) && (w.LatestDate == null || w.LatestDate >= now.Date))
                    .OrderByDescending(w => w.Priority).ThenBy(w => w.CreatedOn)
                    .Select(w => new { Entry = w, w.Patient, w.AppointmentType })
                    .Take(50)
                    .ToList();
                foreach (var e in entries)
                {
                    DateTime from = e.Entry.EarliestDate > now.Date ? e.Entry.EarliestDate : now.Date;
                    int days = e.Entry.LatestDate.HasValue ? Math.Max(1, (e.Entry.LatestDate.Value - from).Days + 1) : 14;
                    AvailableSlot slot = availability.FindSlots(new SlotQuery
                    {
                        FromDate = from,
                        Days = Math.Min(days, 21),
                        AppointmentTypeId = e.Entry.AppointmentTypeId,
                        SiteId = e.Entry.SiteId,
                        ProviderId = e.Entry.ProviderId,
                        MaxResults = 1
                    }, now.AddHours(4)).FirstOrDefault();
                    if (slot == null)
                    {
                        none++;
                        continue;
                    }

                    string text = string.Format(Us, "Southridge Medical Group: an earlier {0} opening is available {1} with {2} at {3}. Call {4} to take it.",
                        e.AppointmentType.Name, slot.Start.ToString("ddd MMM d h:mm tt", Us), slot.ProviderName, slot.SiteName,
                        PhoneNumber.Format(ClinicLookups.Site(slot.SiteId).Phone));
                    try
                    {
                        if (!string.IsNullOrEmpty(e.Patient.MobilePhone) && _settings.GetSetting("Reminders.SmsEnabled", true))
                        {
                            _sms.Send(e.Patient.MobilePhone, text);
                        }
                        else if (!string.IsNullOrEmpty(e.Patient.Email))
                        {
                            _mailer.Send(e.Patient.Email, "An earlier appointment is available", "Dear " + e.Patient.FirstName + ",\r\n\r\n" + text + "\r\n", false);
                        }
                        else
                        {
                            continue;
                        }
                    }
                    catch (Exception ex)
                    {
                        if (!(ex is WebException || ex is SmtpException || ex is ClinicRuleException))
                        {
                            throw;
                        }

                        ClinicLog.Warning(LogCategory.Scheduling, "Waitlist offer to patient " + e.Patient.PatientId + " failed: " + ex.Message);
                        continue;
                    }

                    e.Entry.OfferedOn = now;
                    offered++;
                }

                db.SubmitChanges();
            }

            return offered + " offers sent, " + none + " entries with no open time";
        }

        private static string EmailBody(ReminderCandidate c, string when)
        {
            var body = new StringBuilder();
            body.AppendLine("Dear " + c.FirstName + " " + c.LastName + ",");
            body.AppendLine();
            body.AppendLine("This is a reminder of your appointment:");
            body.AppendLine();
            body.AppendLine("  " + c.AppointmentTypeName + " with " + c.ProviderName);
            body.AppendLine("  " + when);
            body.AppendLine("  " + c.SiteName + ", " + c.SiteAddress);
            body.AppendLine("  Confirmation code " + c.ConfirmationCode);
            body.AppendLine();
            body.AppendLine("Please arrive 15 minutes early and bring your insurance card. You can fill in the pre-visit form");
            body.AppendLine("on the patient site before you come.");
            body.AppendLine();
            body.AppendLine("If you cannot come, please cancel on the patient site or call " + PhoneNumber.Format(c.SitePhone) + " at least 24 hours ahead.");
            body.AppendLine();
            body.AppendLine("Southridge Medical Group");
            return body.ToString();
        }
    }
}
