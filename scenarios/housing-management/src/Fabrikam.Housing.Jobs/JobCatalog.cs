using System;
using System.Collections.Generic;
using System.Linq;
using Fabrikam.Housing.Jobs.Jobs;
using Hangfire;

namespace Fabrikam.Housing.Jobs
{
    /// <summary>
    /// The recurring jobs, their default schedules (cron, UK local time) and how they are started.
    /// Schedules can be overridden in Jobs:Schedules:&lt;name&gt;.
    /// </summary>
    public static class JobCatalog
    {
        public class Entry
        {
            public string Name { get; set; }
            public string DefaultCron { get; set; }
            public Type JobType { get; set; }
            public Action<IRecurringJobManager, string, string, TimeZoneInfo> Register { get; set; }
        }

        private static Entry E<T>(string name, string cron) where T : IHousingJob => new Entry
        {
            Name = name,
            DefaultCron = cron,
            JobType = typeof(T),
            Register = (manager, id, schedule, zone) => manager.AddOrUpdate<T>(id, job => job.RunAsync(), schedule, zone)
        };

        public static readonly IReadOnlyList<Entry> All = new[]
        {
            E<RentChargeRunJob>("rent-charge-run", "30 0 * * 1"),
            E<DirectDebitCollectionsJob>("dd-collections", "0 6 * * *"),
            E<PaymentsSyncJob>("payments-sync", "*/15 * * * *"),
            E<ArrearsReviewJob>("arrears-review", "0 7 * * 1-5"),
            E<ComplianceRemindersJob>("compliance-reminders", "0 8 * * 1-5"),
            E<RepairsSlaMonitorJob>("repairs-sla-monitor", "5 * * * *"),
            E<HousingBenefitImportJob>("hb-schedule-import", "*/30 * * * *"),
            E<ContractorPaymentRunJob>("contractor-payment-run", "0 14 * * 4"),
            E<FinanceExportJob>("finance-export", "0 22 * * *"),
            E<GeocodePropertiesJob>("geocode-properties", "0 2 * * *"),
            E<DocumentRetentionJob>("document-retention", "0 3 1 * *")
        };

        public static Entry Find(string name) => All.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));

        public static void RegisterAll(IRecurringJobManager manager, JobsOptions options)
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(options.TimeZone);
            foreach (var entry in All)
            {
                var cron = options.Schedules != null && options.Schedules.TryGetValue(entry.Name, out var configured) && !string.IsNullOrWhiteSpace(configured)
                    ? configured
                    : entry.DefaultCron;
                if (cron == "disabled")
                    manager.RemoveIfExists(entry.Name);
                else
                    entry.Register(manager, entry.Name, cron, zone);
            }
        }
    }
}
