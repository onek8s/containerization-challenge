using System;
using System.Collections.Generic;
using System.Linq;
using Lucerne.ProjectSites.Contracts.Schema;
using Lucerne.ProjectSites.Core.Mapping;
using Lucerne.ProjectSites.Domain;
using Lucerne.ProjectSites.SharePoint;

namespace Lucerne.ProjectSites.Core.Services
{
    public class RollupResult
    {
        public RollupResult()
        {
            Statuses = new List<ProjectStatus>();
            Failures = new Dictionary<string, string>();
        }

        public List<ProjectStatus> Statuses { get; private set; }
        public Dictionary<string, string> Failures { get; private set; }

        public IDictionary<RagStatus, int> CountByStatus()
        {
            return Statuses.GroupBy(s => s.Overall).ToDictionary(g => g.Key, g => g.Count());
        }
    }

    /// <summary>
    /// Reads every live project site and writes the calculated RAG status to the hub "Project Status" list.
    /// Runs nightly as the Lucerne Project Status Roll-up timer job; the dashboards read the result.
    /// </summary>
    public class StatusRollupService
    {
        public const string ManualStatusProperty = "lucerne_manualstatus";
        public const string ManualStatusDateProperty = "lucerne_manualstatusdate";
        public const string ManualStatusCommentProperty = "lucerne_manualstatuscomment";

        private readonly ISiteConnector _connector;
        private readonly string _hubUrl;
        private readonly StatusAssessor _assessor;
        private readonly IActivityLog _log;

        public StatusRollupService(ISiteConnector connector, string hubUrl, StatusAssessor assessor, IActivityLog log)
        {
            _connector = connector;
            _hubUrl = hubUrl;
            _assessor = assessor ?? new StatusAssessor();
            _log = log ?? NullActivityLog.Instance;
        }

        public RollupResult Run(DateTime today)
        {
            var result = new RollupResult();
            using (var hubSession = _connector.Open(_hubUrl))
            {
                var hub = new HubRepository(hubSession);
                var projects = hub.GetProjects(ProjectStage.Active, ProjectStage.OnHold, ProjectStage.Closing)
                    .Where(p => !string.IsNullOrEmpty(p.Value.SiteUrl))
                    .ToList();

                _log.Info("Status roll-up: " + projects.Count + " project sites");

                foreach (var entry in projects)
                {
                    var project = entry.Value;
                    try
                    {
                        var snapshot = ReadSnapshot(project);
                        var status = _assessor.Assess(snapshot, today);
                        hub.Upsert(ListNames.ProjectStatus, FieldNames.ProjectNumber, project.Number, ListMapper.ToFields(status, project.Name, project.OfficeCode));
                        result.Statuses.Add(status);
                    }
                    catch (Exception ex)
                    {
                        // One broken site must not stop the roll-up of the others.
                        result.Failures[project.Number] = ex.Message;
                        _log.Error("Status roll-up failed for " + project.Number + " (" + project.SiteUrl + ")", ex);
                    }
                }

                hubSession.Commit();
                hub.Log(ListNames.SyncLog, "Status roll-up " + today.ToString("yyyy-MM-dd"), new FieldValues()
                    .Set(FieldNames.Job, "StatusRollup")
                    .Set(FieldNames.Outcome, result.Statuses.Count + " assessed, " + result.Failures.Count + " failed"));
                hubSession.Commit();
            }

            return result;
        }

        public ProjectSnapshot ReadSnapshot(Project project)
        {
            using (var site = _connector.Open(project.SiteUrl))
            {
                var snapshot = new ProjectSnapshot { Project = project };
                snapshot.Deliverables.AddRange(site.GetList(ListNames.Deliverables).Query(CamlQuery.All()).Select(ListMapper.ToDeliverable));
                snapshot.Risks.AddRange(site.GetList(ListNames.Risks)
                    .Query(new CamlQuery { Query = Caml.Where(Caml.Eq(FieldNames.IsOpen, "Boolean", true)) })
                    .Select(ListMapper.ToRisk));
                snapshot.Rfis.AddRange(site.GetList(ListNames.Rfis).Query(CamlQuery.All()).Select(ListMapper.ToRfi));
                snapshot.Actions.AddRange(site.GetList(ListNames.Actions)
                    .Query(new CamlQuery { Query = Caml.Where(Caml.Eq(FieldNames.Completed, "Boolean", false)) })
                    .Select(ListMapper.ToAction));
                snapshot.HoursToDate = site.GetList(ListNames.Hours).Query(new CamlQuery { ViewFields = new[] { FieldNames.Hours } })
                    .Sum(i => i.GetDecimal(FieldNames.Hours));

                RagStatus manual;
                var manualText = site.GetProperty(ManualStatusProperty);
                if (!string.IsNullOrEmpty(manualText) && Enum.TryParse(manualText, true, out manual))
                {
                    DateTime date;
                    snapshot.ManualStatus = manual;
                    snapshot.ManualStatusDate = DateTime.TryParse(site.GetProperty(ManualStatusDateProperty), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out date) ? date : (DateTime?)null;
                    snapshot.ManualStatusComment = site.GetProperty(ManualStatusCommentProperty);
                }

                return snapshot;
            }
        }
    }
}
