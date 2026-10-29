using System;
using System.Configuration;
using System.Globalization;
using Microsoft.Practices.Unity;
using VanArsdel.Integration.Data;
using VanArsdel.Integration.Management;
using VanArsdel.Integration.Management.Wmi;
using VanArsdel.Integration.Monitoring;

namespace VanArsdel.Integration.SlaMonitor
{
    public static class ContainerConfig
    {
        public static IUnityContainer Build()
        {
            IntegrationDatabase.EnsureFactory();
            var settings = MonitorSettings.FromConfig();
            var container = new UnityContainer();
            var integration = new IntegrationDatabase();

            container.RegisterInstance(settings);
            container.RegisterInstance(integration);
            container.RegisterInstance(new InterchangeRepository(integration));
            container.RegisterInstance(new MonitoringRepository(integration));
            container.RegisterInstance(new TrackingRepository(new IntegrationDatabase("BAMPrimaryImport")));
            container.RegisterInstance(new BizTalkDatabaseQueries(new IntegrationDatabase("BizTalkMsgBoxDb")));
            container.RegisterType<IHostInstances>(new ContainerControlledLifetimeManager(), new InjectionFactory(c => new WmiHostInstances(BizTalkGroup.FromConfig().WmiServer)));
            container.RegisterType<IAlertStateStore, MonitoringRepository>();
            container.RegisterType<IMailSender, SmtpMailSender>(new ContainerControlledLifetimeManager());
            container.RegisterType<SnapshotCollector>(new ContainerControlledLifetimeManager());
            container.RegisterType<MonitorLoop>(new ContainerControlledLifetimeManager());
            return container;
        }
    }

    public class MonitorSettings
    {
        public TimeSpan Interval { get; set; }
        public TimeSpan RepeatAfter { get; set; }
        public int EscalateAfter { get; set; }
        public string Environment { get; set; }
        public string OpsPortalUrl { get; set; }
        public string TimeZoneId { get; set; }
        public TimeSpan BusinessOpen { get; set; }
        public TimeSpan BusinessClose { get; set; }
        public string FallbackRecipients { get; set; }
        public string HeartbeatFile { get; set; }

        public static MonitorSettings FromConfig()
        {
            var s = ConfigurationManager.AppSettings;
            return new MonitorSettings
            {
                Interval = TimeSpan.FromMinutes(Int(s["Monitor.IntervalMinutes"], 5)),
                RepeatAfter = TimeSpan.FromMinutes(Int(s["Monitor.RepeatAfterMinutes"], 60)),
                EscalateAfter = Int(s["Monitor.EscalateAfterRepeats"], 3),
                Environment = s["Monitor.Environment"] ?? "PROD",
                OpsPortalUrl = s["Monitor.OpsPortalUrl"],
                TimeZoneId = s["Monitor.TimeZone"] ?? "GMT Standard Time",
                BusinessOpen = TimeSpan.Parse(s["Monitor.BusinessOpen"] ?? "07:00", CultureInfo.InvariantCulture),
                BusinessClose = TimeSpan.Parse(s["Monitor.BusinessClose"] ?? "18:00", CultureInfo.InvariantCulture),
                FallbackRecipients = s["Monitor.FallbackRecipients"],
                HeartbeatFile = s["Monitor.HeartbeatFile"]
            };
        }

        private static int Int(string value, int fallback)
        {
            int i;
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out i) ? i : fallback;
        }
    }
}
