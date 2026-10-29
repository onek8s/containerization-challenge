using System;
using System.Collections.Generic;
using AutoMapper;
using Lucerne.ProjectSites.Core;
using Lucerne.ProjectSites.Data;
using Lucerne.ProjectSites.Integration.DocumentControl;
using Lucerne.ProjectSites.Integration.Erp;
using Lucerne.ProjectSites.Integration.Hr;
using Lucerne.ProjectSites.Integration.Mapping;
using Lucerne.ProjectSites.SharePoint;
using Lucerne.ProjectSites.SharePoint.Auth;
using Lucerne.ProjectSites.SharePoint.Csom;
using Lucerne.ProjectSites.Sync;
using Lucerne.ProjectSites.Sync.Jobs;
using Lucerne.ProjectSites.SyncService.Jobs;
using Unity;
using Unity.Injection;
using Unity.Lifetime;

namespace Lucerne.ProjectSites.SyncService
{
    public static class ContainerConfig
    {
        public static IUnityContainer Build(ServiceSettings settings)
        {
            var c = new UnityContainer();
            var log = new Log4NetActivityLog("Sync");
            c.RegisterInstance(settings);
            c.RegisterInstance(settings.Sync);
            c.RegisterInstance<IActivityLog>(log);
            c.RegisterInstance<IMapper>(IntegrationProfile.CreateMapper());

            c.RegisterType<HighTrustTokenIssuer>(new ContainerControlledLifetimeManager(), new InjectionFactory(_ =>
            {
                var realm = string.IsNullOrEmpty(settings.Realm) ? new RealmDiscovery().GetRealm(new Uri(settings.Sync.HubUrl)) : settings.Realm;
                return new HighTrustTokenIssuer(SigningCertificate.Load(settings.CertificateThumbprint, null, null), settings.IssuerId, settings.ClientId, realm);
            }));
            c.RegisterType<ISiteConnector>(new InjectionFactory(x => new CsomSiteConnector(new HighTrustAppOnlyContextFactory(x.Resolve<HighTrustTokenIssuer>()))));

            c.RegisterType<IErpProjectSource>(new ContainerControlledLifetimeManager(), new InjectionFactory(_ => new ErpProjectClient(settings.Erp, log.Info)));
            c.RegisterType<IHrDirectory>(new InjectionFactory(_ => new HrDirectoryClient(settings.Hr, log.Info)));
            c.RegisterType<IDocumentControlGateway>(new ContainerControlledLifetimeManager(), new InjectionFactory(_ => new DocumentControlClient(settings.DocumentControl, log.Info)));

            c.RegisterType<Func<ProjectSitesContext>>(new InjectionFactory(_ => new Func<ProjectSitesContext>(() => new ProjectSitesContext())));
            c.RegisterType<ISyncStateStore, EfSyncStateStore>();
            c.RegisterType<KpiRepository>();

            // Order matters for a full run: staff first (logins for PM/director), then projects, hours, documents.
            c.RegisterType<ISyncJob, StaffSyncJob>("StaffSync");
            c.RegisterType<ISyncJob, ProjectSyncJob>("ProjectSync");
            c.RegisterType<ISyncJob, TimesheetSyncJob>("TimesheetSync");
            c.RegisterType<ISyncJob, ApprovalPublishJob>("ApprovalPublish");
            c.RegisterType<ISyncJob, KpiSnapshotJob>("KpiSnapshot");
            c.RegisterType<ISyncJob, StatusDigestJob>("StatusDigest");
            c.RegisterType<SyncEngine>(new InjectionConstructor(new ResolvedArrayParameter<ISyncJob>(
                new ResolvedParameter<ISyncJob>("StaffSync"),
                new ResolvedParameter<ISyncJob>("ProjectSync"),
                new ResolvedParameter<ISyncJob>("TimesheetSync"),
                new ResolvedParameter<ISyncJob>("ApprovalPublish"),
                new ResolvedParameter<ISyncJob>("KpiSnapshot"),
                new ResolvedParameter<ISyncJob>("StatusDigest")), typeof(ISyncStateStore), typeof(IActivityLog)));
            return c;
        }
    }

    public sealed class Log4NetActivityLog : IActivityLog
    {
        private readonly log4net.ILog _log;

        public Log4NetActivityLog(string name)
        {
            _log = log4net.LogManager.GetLogger(name);
        }

        public void Info(string message)
        {
            _log.Info(message);
        }

        public void Warn(string message)
        {
            _log.Warn(message);
        }

        public void Error(string message, Exception exception)
        {
            _log.Error(message, exception);
        }
    }
}
