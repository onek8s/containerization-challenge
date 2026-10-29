using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Lucerne.ProjectSites.Contracts.Schema;
using Lucerne.ProjectSites.Core;
using Lucerne.ProjectSites.Core.Services;
using Lucerne.ProjectSites.Domain;
using Lucerne.ProjectSites.SharePoint;

namespace Lucerne.ProjectSites.LabStubs.Seed
{
    /// <summary>
    /// Fills project sites with the content project teams create: deliverables, risks, RFIs, actions and
    /// documents. Document numbers come from the same allocator the ItemAdded receiver uses.
    /// </summary>
    public class SiteContentSeeder
    {
        private static readonly string[] DeliverableTitles =
        {
            "General arrangement", "Foundation layout", "Load assumptions", "Steel connection details", "Drainage strategy",
            "Ground investigation report", "Fire strategy", "Services coordination model", "Earthworks specification",
            "Temporary works scheme", "Bearing schedule", "Deck reinforcement", "Façade support brackets", "Lighting design",
            "Noise assessment", "Construction phasing", "Pile design calculation", "Retaining wall sections", "Ventilation schematic",
            "Cable routing", "Environmental impact summary", "Inspection and maintenance plan", "Precast unit schedule", "Roof bracing"
        };

        private readonly ISiteConnector _connector;
        private readonly DateTime _today;

        public SiteContentSeeder(ISiteConnector connector, DateTime today)
        {
            _connector = connector;
            _today = today.Date;
        }

        public void Seed(SeedProject seed, string siteUrl, IDictionary<string, string> loginsByEmployee, decimal historicHours = 0)
        {
            var p = seed.Erp;
            var random = new Random(p.ProjectNo.GetHashCode() & 0x7fffffff ^ 0x5eed);
            var disciplines = p.DisciplineCodes.Split(',');
            var team = seed.TeamEmployeeNos.Where(loginsByEmployee.ContainsKey).Select(e => loginsByEmployee[e]).ToList();

            using (var site = _connector.Open(siteUrl))
            {
                var deliverables = site.GetList(ListNames.Deliverables);
                var count = 12 + random.Next(14);
                var allocator = new DocumentNumberAllocator(site);
                for (var i = 0; i < count; i++)
                {
                    var discipline = disciplines[i % disciplines.Length];
                    var type = new[] { "DWG", "DWG", "CAL", "RPT", "SPC", "MOD", "SCH" }[random.Next(7)];
                    var number = allocator.Next(p.ProjectNo, discipline, type);
                    var planned = _today.AddDays(-90 + i * 12 + random.Next(10));
                    var slip = seed.Profile == 0 ? random.Next(0, 6) : seed.Profile == 1 ? random.Next(5, 30) : random.Next(15, 60);
                    var status = planned.AddDays(slip) < _today.AddDays(-14)
                        ? (random.Next(10) < (seed.Profile == 2 ? 5 : 9) ? DeliverableStatus.Issued : DeliverableStatus.ForReview)
                        : planned < _today.AddDays(20) ? DeliverableStatus.WorkInProgress : DeliverableStatus.Planned;
                    // Troubled projects carry overdue items whose forecast was never updated
                    var forecast = seed.Profile == 2 && random.Next(3) == 0 ? planned : planned.AddDays(slip);
                    deliverables.Add(new FieldValues()
                        .Set(FieldNames.Title, DeliverableTitles[(i + random.Next(5)) % DeliverableTitles.Length])
                        .Set(FieldNames.DocumentNumber, number.ToString())
                        .Set(FieldNames.Discipline, discipline)
                        .Set(FieldNames.DocumentType, type)
                        .Set(FieldNames.Revision, status == DeliverableStatus.Issued ? "C0" + (1 + random.Next(3)) : "P0" + (1 + random.Next(4)))
                        .Set(FieldNames.DeliverableStatus, status.ToString())
                        .Set(FieldNames.PlannedDate, planned)
                        .Set(FieldNames.ForecastDate, forecast)
                        .Set(FieldNames.IssuedDate, status == DeliverableStatus.Issued ? forecast : (DateTime?)null));
                }

                var risks = site.GetList(ListNames.Risks);
                var riskTitles = new[] { "Late client design freeze", "Unknown utilities in excavation", "Steel price increase", "Permit appeal by neighbours", "Contractor resource shortage", "Groundwater higher than assumed", "Interface with rail possessions" };
                for (var i = 0; i < 3 + random.Next(4); i++)
                {
                    var severity = seed.Profile == 2 && i < 3 ? (i == 0 ? RiskSeverity.Critical : RiskSeverity.High)
                        : seed.Profile == 1 && i == 0 ? RiskSeverity.High
                        : (RiskSeverity)(1 + random.Next(2));
                    risks.Add(new FieldValues()
                        .Set(FieldNames.Title, riskTitles[(i + random.Next(3)) % riskTitles.Length])
                        .Set(FieldNames.Severity, severity.ToString())
                        .Set(FieldNames.IsOpen, i < 4)
                        .Set(FieldNames.Owner, team.Count > 0 ? new UserValue(team[i % team.Count]) : null));
                }

                var rfis = site.GetList(ListNames.Rfis);
                for (var i = 0; i < 4 + random.Next(5); i++)
                {
                    var raised = _today.AddDays(-random.Next(60));
                    rfis.Add(new FieldValues()
                        .Set(FieldNames.Title, "Clarification " + (i + 1) + " on " + DeliverableTitles[random.Next(DeliverableTitles.Length)].ToLowerInvariant())
                        .Set(FieldNames.RfiReference, p.ProjectNo + "-RFI-" + (i + 1).ToString("000", CultureInfo.InvariantCulture))
                        .Set(FieldNames.Raised, raised)
                        .Set(FieldNames.ResponseDue, raised.AddDays(10))
                        .Set(FieldNames.IsOpen, seed.Profile > 0 ? i % 2 == 0 : i % 4 == 0));
                }

                var actions = site.GetList(ListNames.Actions);
                for (var i = 0; i < 5; i++)
                {
                    actions.Add(new FieldValues()
                        .Set(FieldNames.Title, "Action " + (i + 1) + " from design review")
                        .Set(FieldNames.DueDate, _today.AddDays(-10 + i * 5))
                        .Set(FieldNames.Completed, i < 2 && seed.Profile == 0)
                        .Set(FieldNames.AssignedTo, team.Count > 0 ? new UserValue(team[i % team.Count]) : null));
                }

                if (historicHours > 0)
                {
                    // Hours from before the sync window, carried over when the site was migrated in 2017.
                    var week = IsoWeek.FromDate(_today).AddWeeks(-26);
                    site.GetList(ListNames.Hours).Add(new FieldValues()
                        .Set(FieldNames.Title, "carried-forward|" + week)
                        .Set(FieldNames.EmployeeId, "carried-forward")
                        .Set(FieldNames.Week, week.ToString())
                        .Set(FieldNames.Hours, (double)historicHours));
                }

                SeedDocuments(site, p.ProjectNo, disciplines, team, random, allocator);
                site.Commit();
            }
        }

        private void SeedDocuments(ISiteSession site, string projectNumber, string[] disciplines, IList<string> team, Random random, DocumentNumberAllocator allocator)
        {
            var approver = team.Count > 0 ? team[0] : null;
            foreach (var code in disciplines)
            {
                var discipline = Discipline.Find(code);
                var library = site.GetList(discipline.LibraryName);
                for (var i = 0; i < 3; i++)
                {
                    var type = i == 0 ? "DWG" : i == 1 ? "CAL" : "RPT";
                    var number = allocator.Next(projectNumber, code, type);
                    var approved = i == 0;
                    var revision = approved ? "C01" : "P0" + (1 + random.Next(3));
                    var title = discipline.Name + " " + (type == "DWG" ? "drawing" : type == "CAL" ? "calculation" : "report") + " " + (i + 1);
                    library.UploadFile(number + "_" + revision + ".pdf", SeedData.FakeFile(title, number.ToString(), revision), new FieldValues()
                        .Set(FieldNames.Title, title)
                        .Set(FieldNames.DocumentNumber, number.ToString())
                        .Set(FieldNames.Discipline, code)
                        .Set(FieldNames.DocumentType, type)
                        .Set(FieldNames.Revision, revision)
                        .Set(FieldNames.DeliverableStatus, approved ? DeliverableStatus.Approved.ToString() : DeliverableStatus.WorkInProgress.ToString())
                        .Set(FieldNames.ApprovalStatus, approved ? ApprovalStatus.Approved.ToString() : ApprovalStatus.None.ToString())
                        .Set(FieldNames.ApprovedBy, approved && approver != null ? new UserValue(approver) : null)
                        .Set(FieldNames.ApprovedDate, approved ? _today.AddDays(-1) : (DateTime?)null), false);
                }
            }

            // A document imported from the pre-2018 file share with a free-text number: document control refuses it.
            var first = Discipline.Find(disciplines[0]);
            site.GetList(first.LibraryName).UploadFile("legacy-" + first.Code.ToLowerInvariant() + "-calc-17.pdf", SeedData.FakeFile("Legacy calculation", first.Code + "-CALC-17", "C01"), new FieldValues()
                .Set(FieldNames.Title, "Legacy calculation (imported)")
                .Set(FieldNames.DocumentNumber, first.Code + "-CALC-17")
                .Set(FieldNames.Discipline, first.Code)
                .Set(FieldNames.DocumentType, "CAL")
                .Set(FieldNames.Revision, "C01")
                .Set(FieldNames.DeliverableStatus, DeliverableStatus.Approved.ToString())
                .Set(FieldNames.ApprovalStatus, ApprovalStatus.Approved.ToString()), false);
        }

        /// <summary>Closed project from before the sync service existed: site with issued and working documents.</summary>
        public void SeedClosedSite(string siteUrl, string projectNumber, string[] disciplines, DateTime closed)
        {
            using (var site = _connector.Open(siteUrl))
            {
                var allocator = new DocumentNumberAllocator(site);
                foreach (var code in disciplines)
                {
                    var library = site.GetList(Discipline.Find(code).LibraryName);
                    var docs = new[]
                    {
                        new { Type = "DWG", Status = DeliverableStatus.Issued, Rev = "C03" },
                        new { Type = "CAL", Status = DeliverableStatus.Issued, Rev = "C01" },
                        new { Type = "COR", Status = DeliverableStatus.Issued, Rev = "C01" },
                        new { Type = "RPT", Status = DeliverableStatus.WorkInProgress, Rev = "P02" },
                        new { Type = "DWG", Status = DeliverableStatus.Withdrawn, Rev = "P01" }
                    };
                    foreach (var d in docs)
                    {
                        var number = allocator.Next(projectNumber, code, d.Type);
                        library.UploadFile(number + "_" + d.Rev + ".pdf", SeedData.FakeFile("Closed project document", number.ToString(), d.Rev), new FieldValues()
                            .Set(FieldNames.Title, code + " " + d.Type + " " + number.Sequence)
                            .Set(FieldNames.DocumentNumber, number.ToString())
                            .Set(FieldNames.Discipline, code)
                            .Set(FieldNames.DocumentType, d.Type)
                            .Set(FieldNames.Revision, d.Rev)
                            .Set(FieldNames.DeliverableStatus, d.Status.ToString())
                            .Set(FieldNames.IssuedDate, d.Status == DeliverableStatus.Issued ? closed.AddMonths(-6) : (DateTime?)null), false);
                    }
                }

                site.SetProperty(WebProperties.ClosedDate, closed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            }
        }
    }
}
