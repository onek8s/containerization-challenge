using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using Lucerne.ProjectSites.Integration;
using Lucerne.ProjectSites.Sync;

namespace Lucerne.ProjectSites.SyncService
{
    public class ServiceSettings
    {
        public SyncOptions Sync { get; set; }
        public ErpSettings Erp { get; set; }
        public HrSettings Hr { get; set; }
        public DocumentControlSettings DocumentControl { get; set; }
        public string ClientId { get; set; }
        public string IssuerId { get; set; }
        public string Realm { get; set; }
        public string CertificateThumbprint { get; set; }
        public string SmtpHost { get; set; }
        public string MailFrom { get; set; }
        public string DashboardsUrl { get; set; }

        /// <summary>Job name -> interval. A job with interval 0 is not scheduled.</summary>
        public Dictionary<string, TimeSpan> Intervals { get; set; }

        public static ServiceSettings FromConfig()
        {
            var a = ConfigurationManager.AppSettings;
            var settings = new ServiceSettings
            {
                Sync = new SyncOptions
                {
                    HubUrl = a["HubUrl"],
                    TimesheetWeeks = Int(a["TimesheetWeeks"], 12),
                    ServiceLogin = a["ServiceLogin"] ?? "i:0#.w|corp\\svc-ps-sync"
                },
                Erp = new ErpSettings { Endpoint = a["Erp.Endpoint"], UserName = a["Erp.UserName"], Password = a["Erp.Password"] },
                Hr = new HrSettings { BaseUrl = a["Hr.BaseUrl"], ApiKey = a["Hr.ApiKey"] },
                DocumentControl = new DocumentControlSettings { BaseUrl = a["DocControl.BaseUrl"], ClientId = a["DocControl.ClientId"], ClientSecret = a["DocControl.ClientSecret"] },
                ClientId = a["ClientId"],
                IssuerId = a["IssuerId"],
                Realm = a["Realm"],
                CertificateThumbprint = a["SigningCertificateThumbprint"],
                SmtpHost = a["SmtpHost"],
                MailFrom = a["MailFrom"],
                DashboardsUrl = a["DashboardsUrl"],
                Intervals = new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase)
            };

            foreach (var job in new[] { "StaffSync", "ProjectSync", "TimesheetSync", "ApprovalPublish", "KpiSnapshot", "StatusDigest" })
            {
                settings.Intervals[job] = TimeSpan.FromMinutes(Int(a["Interval." + job], 0));
            }

            return settings;
        }

        private static int Int(string value, int fallback)
        {
            int n;
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out n) ? n : fallback;
        }
    }
}
