using System;
using Lucerne.ProjectSites.Core.Services;
using Lucerne.ProjectSites.Domain;
using Lucerne.ProjectSites.Farm.Common;
using Lucerne.ProjectSites.ServerObjectModel;
using Microsoft.SharePoint.Administration;

namespace Lucerne.ProjectSites.Farm.TimerJobs
{
    public class StatusRollupJob : LucerneJobBase
    {
        public const string JobName = "lucerne-project-status-rollup";

        public StatusRollupJob()
        {
        }

        public StatusRollupJob(SPWebApplication webApplication)
            : base(JobName, "Lucerne Project Status Roll-up", webApplication)
        {
        }

        protected override void Run(SsomSiteConnector connector)
        {
            var result = new StatusRollupService(connector, HubUrl, new StatusAssessor(), Log).Run(DateTime.Today);
            UpdateProgress(100);
            Log.Info("Roll-up done: " + result.Statuses.Count + " projects, " + result.Failures.Count + " failed");
        }
    }

    public class DocumentRetentionJob : LucerneJobBase
    {
        public const string JobName = "lucerne-document-retention";

        public DocumentRetentionJob()
        {
        }

        public DocumentRetentionJob(SPWebApplication webApplication)
            : base(JobName, "Lucerne Document Retention", webApplication)
        {
        }

        protected override void Run(SsomSiteConnector connector)
        {
            new RetentionService(connector, HubUrl, new RetentionPolicy(), Log).Run(DateTime.Today, false);
        }
    }

    public class ResourceSyncJob : LucerneJobBase
    {
        public const string JobName = "lucerne-resource-sync";

        public ResourceSyncJob()
        {
        }

        public ResourceSyncJob(SPWebApplication webApplication)
            : base(JobName, "Lucerne Project Team Sync", webApplication)
        {
        }

        protected override void Run(SsomSiteConnector connector)
        {
            new ResourceSyncService(connector, HubUrl, Log).Run(DateTime.Today);
        }
    }

    public class SiteProvisioningJob : LucerneJobBase
    {
        public const string JobName = "lucerne-site-provisioning";

        public SiteProvisioningJob()
        {
        }

        public SiteProvisioningJob(SPWebApplication webApplication)
            : base(JobName, "Lucerne Project Site Provisioning", webApplication)
        {
        }

        protected override void Run(SsomSiteConnector connector)
        {
            var service = new SiteProvisioningService(
                connector,
                new SsomSiteProvisioner(WebApplication),
                HubUrl,
                FarmSettings.WebApplicationUrl(WebApplication),
                Log);
            var result = service.ProcessQueue();
            if (result.Created.Count > 0 || result.Failed.Count > 0)
            {
                Log.Info("Provisioning: " + result.Created.Count + " created, " + result.Failed.Count + " failed, " + result.Rejected + " rejected");
            }
        }
    }
}
