using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Globalization;
using System.Linq;
using StaffOnboarding.Core.Scheduling;

namespace StaffOnboarding.Workflows
{
    public class WorkflowSettings
    {
        public string OnboardingConnectionString { get; set; }
        public string InstanceStoreConnectionString { get; set; }
        public Uri BaseAddress { get; set; }
        public Uri ProvisioningUrl { get; set; }
        public TimeSpan ProvisioningTimeout { get; set; }
        public string OnboardingShare { get; set; }
        public string AccessMapPath { get; set; }
        public string HolidaysPath { get; set; }
        public string OfficeTimeZone { get; set; }
        public string SmtpHost { get; set; }
        public int SmtpPort { get; set; }
        public string MailFrom { get; set; }
        public string MailDomain { get; set; }
        public string ITApprovers { get; set; }
        public string ITEscalation { get; set; }
        public string HRTeam { get; set; }
        public string ServiceDesk { get; set; }
        public string PortalUrl { get; set; }
        public IList<string> AllowedCallers { get; set; }
        public ScheduleOptions Schedule { get; set; }

        public static WorkflowSettings FromConfiguration()
        {
            var app = ConfigurationManager.AppSettings;
            var schedule = new ScheduleOptions();
            schedule.ProvisioningLeadBusinessDays = Int(app, "Schedule.ProvisioningLeadBusinessDays", schedule.ProvisioningLeadBusinessDays);
            schedule.ProvisioningTimeOfDay = Time(app, "Schedule.ProvisioningTimeOfDay", schedule.ProvisioningTimeOfDay);
            schedule.ApprovalReminderInterval = Time(app, "Schedule.ApprovalReminderInterval", schedule.ApprovalReminderInterval);
            schedule.RemindersBeforeEscalation = Int(app, "Schedule.RemindersBeforeEscalation", schedule.RemindersBeforeEscalation);
            schedule.EscalationsBeforeExpiry = Int(app, "Schedule.EscalationsBeforeExpiry", schedule.EscalationsBeforeExpiry);
            schedule.LeaverDisableTimeOfDay = Time(app, "Schedule.LeaverDisableTimeOfDay", schedule.LeaverDisableTimeOfDay);
            schedule.CleanupDelay = Time(app, "Schedule.CleanupDelay", schedule.CleanupDelay);
            schedule.ProvisioningRetryDelay = Time(app, "Schedule.ProvisioningRetryDelay", schedule.ProvisioningRetryDelay);
            schedule.ProvisioningMaxAttempts = Int(app, "Schedule.ProvisioningMaxAttempts", schedule.ProvisioningMaxAttempts);

            return new WorkflowSettings
            {
                OnboardingConnectionString = Connection("Onboarding"),
                InstanceStoreConnectionString = Connection("InstanceStore"),
                BaseAddress = new Uri(Required(app, "BaseAddress")),
                ProvisioningUrl = new Uri(Required(app, "ProvisioningUrl")),
                ProvisioningTimeout = Time(app, "ProvisioningTimeout", TimeSpan.FromSeconds(100)),
                OnboardingShare = Required(app, "OnboardingShare"),
                AccessMapPath = Required(app, "AccessMapPath"),
                HolidaysPath = Required(app, "HolidaysPath"),
                OfficeTimeZone = Required(app, "OfficeTimeZone"),
                SmtpHost = Required(app, "SmtpHost"),
                SmtpPort = Int(app, "SmtpPort", 25),
                MailFrom = Required(app, "MailFrom"),
                MailDomain = Required(app, "MailDomain"),
                ITApprovers = Required(app, "ITApprovers"),
                ITEscalation = Required(app, "ITEscalation"),
                HRTeam = Required(app, "HRTeam"),
                ServiceDesk = Required(app, "ServiceDesk"),
                PortalUrl = Required(app, "PortalUrl").TrimEnd('/'),
                AllowedCallers = Required(app, "AllowedCallers")
                    .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim()).ToList(),
                Schedule = schedule
            };
        }

        private static string Connection(string name)
        {
            var cs = ConfigurationManager.ConnectionStrings[name];
            if (cs == null || string.IsNullOrWhiteSpace(cs.ConnectionString))
                throw new ConfigurationErrorsException("Connection string '" + name + "' is missing.");
            return cs.ConnectionString;
        }

        private static string Required(NameValueCollection app, string key)
        {
            var value = app[key];
            if (string.IsNullOrWhiteSpace(value))
                throw new ConfigurationErrorsException("App setting '" + key + "' is missing.");
            return value.Trim();
        }

        private static int Int(NameValueCollection app, string key, int fallback)
        {
            var value = app[key];
            return string.IsNullOrWhiteSpace(value) ? fallback : int.Parse(value, CultureInfo.InvariantCulture);
        }

        private static TimeSpan Time(NameValueCollection app, string key, TimeSpan fallback)
        {
            var value = app[key];
            return string.IsNullOrWhiteSpace(value) ? fallback : TimeSpan.Parse(value, CultureInfo.InvariantCulture);
        }
    }
}
