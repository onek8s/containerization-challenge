using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Lucerne.ProjectSites.Contracts.Schema;
using Lucerne.ProjectSites.Core;
using Lucerne.ProjectSites.Core.Mapping;
using Lucerne.ProjectSites.Core.Services;
using Lucerne.ProjectSites.Documents;
using Lucerne.ProjectSites.Domain;
using Lucerne.ProjectSites.Integration;
using Lucerne.ProjectSites.Integration.DocumentControl;
using Lucerne.ProjectSites.Integration.Erp;
using Lucerne.ProjectSites.Integration.Hr;
using Lucerne.ProjectSites.Integration.Mapping;
using Lucerne.ProjectSites.LabStubs.InMemory;
using Lucerne.ProjectSites.LabStubs.Seed;
using Lucerne.ProjectSites.LabStubs.Simulators;
using Lucerne.ProjectSites.SharePoint;
using Lucerne.ProjectSites.Sync;
using Lucerne.ProjectSites.Sync.Jobs;
using Newtonsoft.Json;

namespace Lucerne.ProjectSites.LabStubs
{
    /// <summary>
    /// End-to-end run in simulator mode: HR/ERP/document-control simulators over HTTP, the in-memory farm in
    /// place of SharePoint, and the real sync jobs, provisioning, team sync, roll-up, retention and reports.
    /// </summary>
    public class LabRunner
    {
        public const string WebApplicationUrl = "http://projects.corp.local";
        public const string HubUrl = WebApplicationUrl + "/sites/hub";

        private readonly DateTime _today;
        private readonly string _output;
        private readonly int _port;
        private readonly List<string> _failures = new List<string>();

        public LabRunner(DateTime today, string output, int port)
        {
            _today = today.Date;
            _output = output;
            _port = port;
        }

        public InMemoryFarm Farm { get; private set; }

        public async Task<int> RunAsync()
        {
            Directory.CreateDirectory(_output);
            RetryPolicies.DelayProvider = _ => TimeSpan.FromMilliseconds(100);
            var log = new ConsoleActivityLog();
            var seed = new SeedData(_today);

            await using var sims = new SimulatorHost(seed, _port);
            await sims.StartAsync();
            Step("Simulators listening on " + sims.BaseUrl + " (ERP SOAP, HR REST, document control)");

            Farm = new InMemoryFarm();
            Farm.CreateHub(HubUrl);
            var options = new SyncOptions { HubUrl = HubUrl, TimesheetWeeks = 12 };
            var state = new InMemorySyncStateStore();
            var mapper = IntegrationProfile.CreateMapper();
            var erp = new ErpProjectClient(new ErpSettings { Endpoint = sims.BaseUrl + "/ProjectAccounting/ProjectService.svc" }, log.Info);
            var hr = new HrDirectoryClient(new HrSettings { BaseUrl = sims.BaseUrl + "/hrapi", ApiKey = SimulatorHost.HrApiKey, PageSize = 25 }, log.Info);
            var docControl = new DocumentControlClient(new DocumentControlSettings { BaseUrl = sims.BaseUrl + "/doccontrol", ClientId = SimulatorHost.DocControlClientId, ClientSecret = SimulatorHost.DocControlSecret }, log.Info);
            var provisioning = new SiteProvisioningService(Farm, Farm, HubUrl, WebApplicationUrl, log) { MaxRequestsPerRun = 25 };
            var seeder = new SiteContentSeeder(Farm, _today);

            // Historic data: a closed project whose site predates the sync service.
            var closed = seed.Projects.Single(p => p.Erp.StatusCode == 50);
            var closedProject = mapper.Map<Project>(closed.Erp);
            var closedPlan = provisioning.Plan(closedProject, "i:0#.w|corp\\spadmin");
            provisioning.Execute(closedPlan);
            seeder.SeedClosedSite(closedPlan.Site.Url, closedProject.Number, closedProject.Disciplines.ToArray(), closed.Erp.ClosedDate.Value);
            using (var hub = Farm.Open(HubUrl))
            {
                hub.GetList(ListNames.Projects).Add(new FieldValues().Set(FieldNames.ProjectNumber, closedProject.Number).Set(FieldNames.SiteUrl, closedPlan.Site.Url));
            }

            Step("Historic closed project site " + closedPlan.Site.Url);

            var engine = new SyncEngine(new ISyncJob[]
            {
                new StaffSyncJob(hr, Farm, mapper, state, options, log),
                new ProjectSyncJob(erp, Farm, mapper, state, options, log)
            }, state, log) { Clock = () => seed.NowUtc.AddHours(1) };
            var runs = engine.RunAll();
            Expect(runs.All(r => r.Succeeded), "staff and project sync succeeded");
            var hubSite = Farm.Get(HubUrl);
            Expect(hubSite.List(ListNames.StaffDirectory).Count == 80, "80 staff with domain accounts in the directory (contractors skipped), got " + hubSite.List(ListNames.StaffDirectory).Count);
            Expect(hubSite.List(ListNames.Projects).Count == seed.Projects.Count, "all ERP projects in the register");
            Step("Staff and project sync: " + Describe(runs));

            var provisioned = provisioning.ProcessQueue();
            Expect(provisioned.Failed.Count == 0, "no provisioning failures");
            Expect(provisioned.Created.Count == seed.Projects.Count(p => p.Erp.StatusCode == 20), "a site for every active project, got " + provisioned.Created.Count);
            Step("Provisioned " + provisioned.Created.Count + " project sites");

            var logins = seed.Employees.Where(e => e.WindowsAccount != null).ToDictionary(e => e.PersonnelNumber, e => Claims.WindowsLogin(e.WindowsAccount));
            using (var hub = Farm.Open(HubUrl))
            {
                foreach (var entry in new HubRepository(hub).GetProjects(ProjectStage.Active))
                {
                    var sp = seed.Projects.Single(p => p.Erp.ProjectNo == entry.Value.Number);
                    seeder.Seed(sp, entry.Value.SiteUrl, logins, seed.HistoricHours(sp));
                }
            }

            // On hold / closing projects had sites before; give them one too so hours and status flow.
            foreach (var p in seed.Projects.Where(x => x.Erp.StatusCode == 30 || x.Erp.StatusCode == 40))
            {
                var project = mapper.Map<Project>(p.Erp);
                var plan = provisioning.Plan(project, "i:0#.w|corp\\spadmin");
                provisioning.Execute(plan);
                seeder.Seed(p, plan.Site.Url, logins, seed.HistoricHours(p));
                using (var hub = Farm.Open(HubUrl))
                {
                    var entry = new HubRepository(hub).FindProject(project.Number).Value;
                    hub.GetList(ListNames.Projects).Update(entry.Key, new FieldValues().Set(FieldNames.SiteUrl, plan.Site.Url), true);
                }
            }

            Step("Seeded deliverables, risks, RFIs, actions and documents");

            var publish = new ApprovalPublishJob(docControl, Farm, options, log);
            var engine2 = new SyncEngine(new ISyncJob[] { new TimesheetSyncJob(erp, Farm, options, log), publish, publish, publish }, state, log) { Clock = () => seed.NowUtc.AddHours(2) };
            runs = engine2.RunAll();
            Expect(runs.All(r => r.Succeeded), "timesheet and approval jobs succeeded");
            Step("Timesheets and document control: " + Describe(runs));

            var docs = AllDocuments().ToList();
            var registered = docs.Count(d => d.GetString(FieldNames.DocControlStatus) == "Registered");
            var rejected = docs.Count(d => d.GetString(FieldNames.ApprovalStatus) == "Rejected");
            Expect(registered > 0 && sims.Submissions.Values.Count(s => !s.WillReject) == registered, "approved documents registered in document control (" + registered + ")");
            Expect(rejected > 0 && rejected == sims.Submissions.Values.Count(s => s.WillReject), "legacy-numbered documents rejected (" + rejected + ")");

            var team = new ResourceSyncService(Farm, HubUrl, log).Run(_today);
            Expect(team.MembersAdded > 0, "team members added from timesheets");
            Step(string.Format("Team sync: {0} sites, {1} members added, {2} removed", team.SitesProcessed, team.MembersAdded, team.MembersRemoved));

            // An engineer leaves (HR marks the record inactive): the next team sync removes them from the sites
            // they were added to, and adds nobody new.
            string leaverId;
            using (var hub = Farm.Open(HubUrl))
            {
                var siteUrl = new HubRepository(hub).FindProject("P18007").Value.Value.SiteUrl;
                using (var site = Farm.Open(siteUrl))
                {
                    leaverId = site.GetList(ListNames.Team).Query(CamlQuery.All())
                        .First(r => r.GetString(FieldNames.TeamRole) == ResourceSyncService.BookedRole)
                        .GetString(FieldNames.EmployeeId);
                }

                var row = new HubRepository(hub).FindByText(ListNames.StaffDirectory, FieldNames.EmployeeId, leaverId);
                hub.GetList(ListNames.StaffDirectory).Update(row.Id, new FieldValues().Set(FieldNames.Active, false), true);
            }

            var again = new ResourceSyncService(Farm, HubUrl, log).Run(_today);
            Expect(again.MembersAdded == 0 && again.MembersRemoved >= 1, "second team sync removes the leaver and adds nobody");
            Step(string.Format("Team sync after leaver {0}: {1} added, {2} removed", leaverId, again.MembersAdded, again.MembersRemoved));

            var rollup = new StatusRollupService(Farm, HubUrl, new StatusAssessor(), log).Run(_today);
            var counts = rollup.CountByStatus();
            Expect(rollup.Failures.Count == 0, "roll-up without failures");
            Expect(counts.ContainsKey(RagStatus.Red) && counts.ContainsKey(RagStatus.Green), "portfolio has green and red projects");
            Step("Status roll-up: " + string.Join(", ", counts.OrderBy(c => c.Key).Select(c => c.Value + " " + c.Key)));

            var preview = new RetentionService(Farm, HubUrl, new RetentionPolicy(), log).Run(_today, true);
            var applied = new RetentionService(Farm, HubUrl, new RetentionPolicy(), log).Run(_today, false);
            Expect(applied.Count(RetentionAction.DeclareRecord) > 0 && applied.Count(RetentionAction.Delete) > 0, "retention declared records and deleted working files");
            var again2 = new RetentionService(Farm, HubUrl, new RetentionPolicy(), log).Run(_today, false);
            Expect(again2.Count(RetentionAction.DeclareRecord) == 0 && again2.Count(RetentionAction.Delete) == 0, "retention is idempotent");
            Step(string.Format("Retention on {0}: preview {1} actions; applied {2} records, {3} deleted, {4} kept",
                closedProject.Number, preview.Decisions.Count(d => d.Action != RetentionAction.Keep),
                applied.Count(RetentionAction.DeclareRecord), applied.Count(RetentionAction.Delete), applied.Count(RetentionAction.Keep)));

            // ERP closes a project: the next incremental sync updates the register and the site property bag.
            var closing = seed.Projects.Single(p => p.Erp.ProjectNo == "P17089");
            closing.Erp.StatusCode = 50;
            closing.Erp.ClosedDate = _today;
            closing.Erp.LastChangedUtc = seed.NowUtc.AddHours(3);
            var incremental = new ProjectSyncJob(erp, Farm, mapper, state, options, log).Run(seed.NowUtc.AddHours(4));
            // The watermark overlap re-reads the most recent earlier change as well.
            Expect(incremental.Created == 0 && incremental.Updated >= 1 && incremental.Updated <= 2, "incremental project sync read only the latest changes, got " + incremental.Updated);
            var closingSite = Farm.Sites.Single(s => s.Url.EndsWith("/p17089", StringComparison.Ordinal));
            Expect(closingSite.Properties.TryGetValue(WebProperties.ProjectStage, out var stage) && stage == "Closed", "closed stage pushed to the project site");
            Step("Incremental project sync: P17089 closed in ERP -> register and site updated");

            WriteReports(seed);
            WriteState();
            Step("Reports and farm state written to " + _output);

            Console.WriteLine();
            if (_failures.Count > 0)
            {
                Console.WriteLine("FAILED checks:");
                _failures.ForEach(f => Console.WriteLine("  - " + f));
                return 1;
            }

            Console.WriteLine("All checks passed.");
            return 0;
        }

        private IEnumerable<ListItemData> AllDocuments()
        {
            foreach (var site in Farm.Sites)
            {
                foreach (var d in Discipline.All.Where(x => site.HasList(x.LibraryName)))
                {
                    foreach (var item in site.List(d.LibraryName).Query(CamlQuery.All()))
                    {
                        yield return item;
                    }
                }
            }
        }

        private void WriteReports(SeedData seed)
        {
            using (var hub = Farm.Open(HubUrl))
            {
                var repo = new HubRepository(hub);
                var statuses = hub.GetList(ListNames.ProjectStatus).Query(CamlQuery.All()).Select(ListMapper.ToStatus).ToDictionary(s => s.ProjectNumber);
                var rows = repo.GetProjects(ProjectStage.Active, ProjectStage.OnHold, ProjectStage.Closing)
                    .Select(p => new PortfolioRow { Project = p.Value, Status = statuses.TryGetValue(p.Value.Number, out var s) ? s : null })
                    .ToList();
                File.WriteAllBytes(Path.Combine(_output, "portfolio.xlsx"), Workbooks.Portfolio(rows, _today));

                var troubled = repo.FindProject("P17112").Value.Value;
                var rollup = new StatusRollupService(Farm, HubUrl, null, null);
                var snapshot = rollup.ReadSnapshot(troubled);
                var status = new StatusAssessor().Assess(snapshot, _today);
                File.WriteAllBytes(Path.Combine(_output, "P17112-status-report.docx"), StatusReportDocument.Create(snapshot, status, "Lab Runner", _today));

                var tower = repo.FindProject("P18007").Value.Value;
                File.WriteAllBytes(Path.Combine(_output, "P18007-deliverables.xlsx"), Workbooks.DeliverableRegister("P18007", rollup.ReadSnapshot(tower).Deliverables, _today));

                var staff = repo.GetStaffByEmployeeId();
                var hours = new List<HoursRow>();
                foreach (var p in rows.Where(r => !string.IsNullOrEmpty(r.Project.SiteUrl)))
                {
                    using (var site = Farm.Open(p.Project.SiteUrl))
                    {
                        hours.AddRange(site.GetList(ListNames.Hours).Query(CamlQuery.All()).Select(h => new HoursRow
                        {
                            ProjectNumber = p.Project.Number,
                            EmployeeId = h.GetString(FieldNames.EmployeeId),
                            EmployeeName = staff.TryGetValue(h.GetString(FieldNames.EmployeeId) ?? string.Empty, out var m) ? m.DisplayName : null,
                            Office = h.GetString(FieldNames.Office),
                            Discipline = h.GetString(FieldNames.Discipline),
                            Week = IsoWeek.Parse(h.GetString(FieldNames.Week)),
                            Hours = h.GetDecimal(FieldNames.Hours)
                        }));
                    }
                }

                File.WriteAllBytes(Path.Combine(_output, "hours-by-week.xlsx"), Workbooks.HoursByWeek(hours));
                File.WriteAllBytes(Path.Combine(_output, "P18007-TRN-0001.docx"), TransmittalSheet.Create("P18007", tower.Name, "P18007-LUC-PMG-TRN-0001", "Northgate Property Partners AG", "Lucerne Engineering", _today,
                    snapshot.Deliverables.Where(d => d.Status == DeliverableStatus.Issued).Take(8)
                        .Select(d => new TransmittalLine { DocumentNumber = d.DocumentNumber, Title = d.Title, Revision = d.Revision, Purpose = "For construction" }).ToList()));
            }
        }

        private void WriteState()
        {
            var export = Farm.Sites.OrderBy(s => s.Url).Select(s => new
            {
                s.Url,
                s.Title,
                Properties = s.Properties,
                Groups = s.Groups.ToDictionary(g => g.Key, g => g.Value.OrderBy(x => x).ToList()),
                Lists = s.Lists.ToDictionary(l => l.Title, l => l.Query(CamlQuery.All()).Select(i => i.Values).ToList())
            });
            File.WriteAllText(Path.Combine(_output, "farm-state.json"), JsonConvert.SerializeObject(export, Formatting.Indented));
        }

        private void Expect(bool condition, string what)
        {
            if (!condition)
            {
                _failures.Add(what);
                Console.WriteLine("  !! " + what);
            }
        }

        private static void Step(string text)
        {
            Console.WriteLine("== " + text);
        }

        private static string Describe(IEnumerable<SyncRun> runs)
        {
            return string.Join("; ", runs.Select(r => string.Format(CultureInfo.InvariantCulture, "{0} +{1}/~{2}/!{3}", r.Job, r.Created, r.Updated, r.Failed)));
        }
    }

    public sealed class ConsoleActivityLog : IActivityLog
    {
        public bool Verbose { get; set; }

        public void Info(string message)
        {
            if (Verbose)
            {
                Console.WriteLine("   " + message);
            }
        }

        public void Warn(string message)
        {
            Console.WriteLine("   WARN " + message);
        }

        public void Error(string message, Exception exception)
        {
            Console.WriteLine("   ERROR " + message + ": " + exception?.GetBaseException().Message);
        }
    }
}
