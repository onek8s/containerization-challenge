using System;
using System.Linq;
using Lucerne.ProjectSites.Contracts.Schema;
using Lucerne.ProjectSites.Core.Services;
using Lucerne.ProjectSites.ServerObjectModel;
using Microsoft.SharePoint.Administration;

namespace Lucerne.ProjectSites.Claims
{
    /// <summary>Copies office and discipline from the hub Staff Directory into the farm directory cache (hourly).</summary>
    public class DirectoryCacheJob : SPJobDefinition
    {
        public const string JobName = "lucerne-claims-directory-cache";

        public DirectoryCacheJob()
        {
        }

        public DirectoryCacheJob(SPWebApplication webApplication)
            : base(JobName, webApplication, null, SPJobLockType.Job)
        {
            Title = "Lucerne Claims Directory Cache";
        }

        public override void Execute(Guid targetInstanceId)
        {
            var log = new UlsActivityLog(JobName);
            var hubUrl = WebApplication.Properties[WebProperties.WebAppHubUrl] as string;
            if (string.IsNullOrEmpty(hubUrl))
            {
                log.Warn("Hub URL not configured on " + WebApplication.Name);
                return;
            }

            using (var hub = new SsomSiteConnector(false).Open(hubUrl))
            {
                var staff = new HubRepository(hub).GetStaffByEmployeeId().Values.ToList();
                var cache = DirectoryCache.EnsureLocal();
                cache.Replace(staff);
                cache.Update();
                log.Info("Directory cache refreshed: " + cache.Count + " accounts");
            }
        }
    }

    public class DirectoryCacheJobFeatureReceiver : Microsoft.SharePoint.SPFeatureReceiver
    {
        public override void FeatureActivated(Microsoft.SharePoint.SPFeatureReceiverProperties properties)
        {
            var webApp = (SPWebApplication)properties.Feature.Parent;
            Remove(webApp);
            new DirectoryCacheJob(webApp) { Schedule = new Microsoft.SharePoint.SPHourlySchedule { BeginMinute = 40, EndMinute = 50 } }.Update();
        }

        public override void FeatureDeactivating(Microsoft.SharePoint.SPFeatureReceiverProperties properties)
        {
            Remove((SPWebApplication)properties.Feature.Parent);
        }

        private static void Remove(SPWebApplication webApp)
        {
            foreach (var job in webApp.JobDefinitions.Where(j => j.Name == DirectoryCacheJob.JobName).ToList())
            {
                job.Delete();
            }
        }
    }
}
