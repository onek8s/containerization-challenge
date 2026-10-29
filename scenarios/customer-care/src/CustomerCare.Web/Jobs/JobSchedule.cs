using System;
using Hangfire;
using Tailspin.CustomerCare.Data;

namespace Tailspin.CustomerCare.Web.Jobs
{
    public static class JobSchedule
    {
        public static void Register()
        {
            var tz = CareSettings.TimeZone;
            var options = new RecurringJobOptions { TimeZone = tz };

            RecurringJob.AddOrUpdate<SlaEscalationJob>("sla-escalation", j => j.Run(), "*/5 * * * *", options);
            RecurringJob.AddOrUpdate<DailyDigestJob>("daily-digest", j => j.Run(), "0 7 * * 1-5", options);
            RecurringJob.AddOrUpdate<StatsRollupJob>("stats-rollup", j => j.Run(), "15 0 * * *", options);
            RecurringJob.AddOrUpdate<WorkFolderCleanupJob>("work-folder-cleanup", j => j.Run(), Cron.Hourly(), options);
        }
    }
}
