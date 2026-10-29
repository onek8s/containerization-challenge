using System;

namespace StaffOnboarding.Core.Scheduling
{
    public class ScheduleOptions
    {
        public ScheduleOptions()
        {
            ProvisioningLeadBusinessDays = 3;
            ProvisioningTimeOfDay = new TimeSpan(6, 0, 0);
            ApprovalReminderInterval = TimeSpan.FromDays(2);
            RemindersBeforeEscalation = 2;
            EscalationsBeforeExpiry = 1;
            LeaverDisableTimeOfDay = new TimeSpan(18, 0, 0);
            CleanupDelay = TimeSpan.FromDays(30);
            ProvisioningRetryDelay = TimeSpan.FromMinutes(15);
            ProvisioningMaxAttempts = 8;
        }

        /// <summary>Accounts are created this many business days before the start date.</summary>
        public int ProvisioningLeadBusinessDays { get; set; }

        public TimeSpan ProvisioningTimeOfDay { get; set; }
        public TimeSpan ApprovalReminderInterval { get; set; }
        public int RemindersBeforeEscalation { get; set; }
        public int EscalationsBeforeExpiry { get; set; }
        public TimeSpan LeaverDisableTimeOfDay { get; set; }
        public TimeSpan CleanupDelay { get; set; }
        public TimeSpan ProvisioningRetryDelay { get; set; }
        public int ProvisioningMaxAttempts { get; set; }
    }
}
