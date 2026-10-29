using System;
using System.Linq;
using Lucerne.ProjectSites.Contracts.Schema;
using Lucerne.ProjectSites.Core;
using Lucerne.ProjectSites.Domain;
using Lucerne.ProjectSites.Farm.Receivers;
using Lucerne.ProjectSites.Farm.TimerJobs;
using Microsoft.SharePoint;
using Microsoft.SharePoint.Administration;

namespace Lucerne.ProjectSites.Farm.Features
{
    /// <summary>Lucerne.ProjectSites.TimerJobs (WebApplication scope): installs and removes the timer jobs.</summary>
    public class TimerJobsFeatureReceiver : SPFeatureReceiver
    {
        public override void FeatureActivated(SPFeatureReceiverProperties properties)
        {
            var webApp = (SPWebApplication)properties.Feature.Parent;
            RemoveJobs(webApp);

            new StatusRollupJob(webApp) { Schedule = new SPDailySchedule { BeginHour = 2, EndHour = 3 } }.Update();
            new DocumentRetentionJob(webApp) { Schedule = new SPWeeklySchedule { BeginDayOfWeek = DayOfWeek.Sunday, EndDayOfWeek = DayOfWeek.Sunday, BeginHour = 3, EndHour = 5 } }.Update();
            new ResourceSyncJob(webApp) { Schedule = new SPHourlySchedule { BeginMinute = 15, EndMinute = 25 } }.Update();
            new SiteProvisioningJob(webApp) { Schedule = new SPMinuteSchedule { Interval = 5, BeginSecond = 0, EndSecond = 59 } }.Update();
        }

        public override void FeatureDeactivating(SPFeatureReceiverProperties properties)
        {
            RemoveJobs((SPWebApplication)properties.Feature.Parent);
        }

        private static void RemoveJobs(SPWebApplication webApp)
        {
            var names = new[] { StatusRollupJob.JobName, DocumentRetentionJob.JobName, ResourceSyncJob.JobName, SiteProvisioningJob.JobName };
            foreach (var job in webApp.JobDefinitions.Where(j => names.Contains(j.Name)).ToList())
            {
                job.Delete();
            }
        }
    }

    /// <summary>Lucerne.ProjectSites.Hub (Web scope): hub lists, receivers on Site Requests, list views.</summary>
    public class HubFeatureReceiver : SPFeatureReceiver
    {
        public override void FeatureActivated(SPFeatureReceiverProperties properties)
        {
            var web = (SPWeb)properties.Feature.Parent;
            var requests = web.Lists[ListNames.SiteRequests];
            ReceiverRegistration.Ensure(requests, typeof(SiteRequestReceiver), SPEventReceiverType.ItemAdding, SPEventReceiverSynchronization.Synchronous);
            ReceiverRegistration.Ensure(requests, typeof(SiteRequestReceiver), SPEventReceiverType.ItemAdded, SPEventReceiverSynchronization.Synchronous);

            // Index the key columns; the lists grow past the 5000 item view threshold (hours, logs).
            Index(web.Lists[ListNames.Projects], FieldNames.ProjectNumber, FieldNames.ProjectStage);
            Index(web.Lists[ListNames.ProjectStatus], FieldNames.ProjectNumber);
            Index(web.Lists[ListNames.StaffDirectory], FieldNames.EmployeeId);
            Index(requests, FieldNames.RequestStatus);
            web.AllProperties[WebProperties.HubUrl] = web.Url;
            web.Update();
        }

        internal static void Index(SPList list, params string[] fields)
        {
            foreach (var name in fields)
            {
                var field = list.Fields.GetFieldByInternalName(name);
                if (!field.Indexed)
                {
                    field.Indexed = true;
                    field.Update();
                }
            }
        }
    }

    /// <summary>
    /// Lucerne.ProjectSites.ProjectSite (Web scope, activated by the web template): discipline libraries,
    /// document receivers, navigation. The disciplines come from the property bag set at provisioning.
    /// </summary>
    public class ProjectSiteFeatureReceiver : SPFeatureReceiver
    {
        public override void FeatureActivated(SPFeatureReceiverProperties properties)
        {
            var web = (SPWeb)properties.Feature.Parent;
            var disciplines = (web.Site.RootWeb.AllProperties["lucerne_disciplines"] as string ?? string.Empty)
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            var engineeringDocument = web.Site.RootWeb.ContentTypes[new SPContentTypeId(ContentTypeIds.EngineeringDocument)];

            foreach (var discipline in ProjectSiteSchema.DisciplineLibraries(disciplines))
            {
                var list = web.Lists.TryGetList(discipline.LibraryName);
                if (list == null)
                {
                    var id = web.Lists.Add(discipline.LibraryName, discipline.Name + " documents", SPListTemplateType.DocumentLibrary);
                    list = web.Lists[id];
                    list.EnableVersioning = true;
                    list.EnableMinorVersions = true;
                    list.MajorVersionLimit = 50;
                    list.ForceCheckout = false;
                    list.ContentTypesEnabled = true;
                    list.OnQuickLaunch = true;
                    list.Update();
                }

                if (engineeringDocument != null && list.ContentTypes[engineeringDocument.Name] == null)
                {
                    list.ContentTypes.Add(engineeringDocument);
                    list.Update();
                }

                ReceiverRegistration.Ensure(list, typeof(DocumentReceiver), SPEventReceiverType.ItemAdding, SPEventReceiverSynchronization.Synchronous);
                ReceiverRegistration.Ensure(list, typeof(DocumentReceiver), SPEventReceiverType.ItemAdded, SPEventReceiverSynchronization.Synchronous);
                ReceiverRegistration.Ensure(list, typeof(DocumentReceiver), SPEventReceiverType.ItemUpdating, SPEventReceiverSynchronization.Synchronous);
                ReceiverRegistration.Ensure(list, typeof(DocumentReceiver), SPEventReceiverType.ItemDeleting, SPEventReceiverSynchronization.Synchronous);
                ReceiverRegistration.Ensure(list, typeof(DocumentReceiver), SPEventReceiverType.ItemCheckingIn, SPEventReceiverSynchronization.Synchronous);
                HubFeatureReceiver.Index(list, FieldNames.DocumentNumber, FieldNames.ApprovalStatus);
            }

            ReceiverRegistration.Ensure(web.Lists[ListNames.Deliverables], typeof(DeliverableReceiver), SPEventReceiverType.ItemUpdating, SPEventReceiverSynchronization.Synchronous);
            ReceiverRegistration.Ensure(web.Lists[ListNames.Risks], typeof(RiskReceiver), SPEventReceiverType.ItemAdded, SPEventReceiverSynchronization.Asynchronous);
            ReceiverRegistration.Ensure(web.Lists[ListNames.Risks], typeof(RiskReceiver), SPEventReceiverType.ItemUpdated, SPEventReceiverSynchronization.Asynchronous);

            web.AllProperties[WebProperties.TemplateVersion] = TemplateNames.CurrentVersion.ToString();
            web.Update();
        }
    }

    internal static class ReceiverRegistration
    {
        public static void Ensure(SPList list, Type receiver, SPEventReceiverType type, SPEventReceiverSynchronization sync)
        {
            var assembly = receiver.Assembly.FullName;
            if (list.EventReceivers.OfType<SPEventReceiverDefinition>().Any(r => r.Class == receiver.FullName && r.Type == type))
            {
                return;
            }

            var definition = list.EventReceivers.Add();
            definition.Name = receiver.Name + " " + type;
            definition.Type = type;
            definition.Assembly = assembly;
            definition.Class = receiver.FullName;
            definition.Synchronization = sync;
            definition.SequenceNumber = 10000;
            definition.Update();
        }
    }
}
