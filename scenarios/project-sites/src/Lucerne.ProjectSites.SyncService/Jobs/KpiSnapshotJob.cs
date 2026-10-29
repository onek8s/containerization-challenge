using System;
using System.Linq;
using Lucerne.ProjectSites.Contracts.Schema;
using Lucerne.ProjectSites.Core.Mapping;
using Lucerne.ProjectSites.Core.Services;
using Lucerne.ProjectSites.Data;
using Lucerne.ProjectSites.Data.Entities;
using Lucerne.ProjectSites.Domain;
using Lucerne.ProjectSites.SharePoint;
using Lucerne.ProjectSites.Sync;

namespace Lucerne.ProjectSites.SyncService.Jobs
{
    /// <summary>Copies the hub "Project Status" list into the dashboards database once a day (KPI history charts).</summary>
    public class KpiSnapshotJob : ISyncJob
    {
        private readonly ISiteConnector _connector;
        private readonly KpiRepository _kpis;
        private readonly SyncOptions _options;

        public KpiSnapshotJob(ISiteConnector connector, KpiRepository kpis, SyncOptions options)
        {
            _connector = connector;
            _kpis = kpis;
            _options = options;
        }

        public string Name
        {
            get { return "KpiSnapshot"; }
        }

        public SyncRun Run(DateTime nowUtc)
        {
            var run = new SyncRun();
            using (var hub = _connector.Open(_options.HubUrl))
            {
                var projects = new HubRepository(hub).GetProjects().ToDictionary(p => p.Value.Number, p => p.Value);
                foreach (var item in hub.GetList(ListNames.ProjectStatus).Query(CamlQuery.All()))
                {
                    var s = ListMapper.ToStatus(item);
                    Project p;
                    projects.TryGetValue(s.ProjectNumber ?? string.Empty, out p);
                    _kpis.Save(new KpiSnapshot
                    {
                        ProjectNumber = s.ProjectNumber,
                        SnapshotDate = nowUtc.Date,
                        OfficeCode = p?.OfficeCode,
                        Overall = s.Overall.ToString(),
                        Schedule = s.Schedule.ToString(),
                        Budget = s.Budget.ToString(),
                        Risk = s.Risk.ToString(),
                        HoursToDate = s.HoursToDate,
                        BudgetHours = s.BudgetHours,
                        BurnRatio = s.BurnRatio,
                        PercentComplete = p?.PercentComplete ?? 0,
                        DeliverablesTotal = s.DeliverablesTotal,
                        DeliverablesIssued = s.DeliverablesIssued,
                        DeliverablesOverdue = s.DeliverablesOverdue,
                        OpenRfis = s.OpenRfis,
                        OpenHighRisks = s.OpenHighRisks
                    });
                    run.Created++;
                }
            }

            return run;
        }
    }
}
