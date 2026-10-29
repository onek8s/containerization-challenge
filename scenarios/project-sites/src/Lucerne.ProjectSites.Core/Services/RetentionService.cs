using System;
using System.Collections.Generic;
using System.Linq;
using Lucerne.ProjectSites.Contracts.Schema;
using Lucerne.ProjectSites.Domain;
using Lucerne.ProjectSites.SharePoint;

namespace Lucerne.ProjectSites.Core.Services
{
    public class RetentionRunResult
    {
        public RetentionRunResult()
        {
            Decisions = new List<RetentionDecision>();
        }

        public List<RetentionDecision> Decisions { get; private set; }
        public int SitesProcessed { get; set; }

        public int Count(RetentionAction action)
        {
            return Decisions.Count(d => d.Action == action);
        }
    }

    /// <summary>
    /// Applies the file plan to closed projects: declares records at close-out, moves records to the archive
    /// library at the end of the liability period and recycles working files. Weekly timer job; the
    /// RetentionPreview.aspx page runs it with whatIf=true for one project.
    /// </summary>
    public class RetentionService
    {
        private readonly ISiteConnector _connector;
        private readonly string _hubUrl;
        private readonly RetentionPolicy _policy;
        private readonly IActivityLog _log;

        public RetentionService(ISiteConnector connector, string hubUrl, RetentionPolicy policy, IActivityLog log)
        {
            _connector = connector;
            _hubUrl = hubUrl;
            _policy = policy ?? new RetentionPolicy();
            _log = log ?? NullActivityLog.Instance;
        }

        public RetentionRunResult Run(DateTime today, bool whatIf, string onlyProject = null)
        {
            var result = new RetentionRunResult();
            using (var hubSession = _connector.Open(_hubUrl))
            {
                var hub = new HubRepository(hubSession);
                var closed = hub.GetProjects(ProjectStage.Closed)
                    .Select(p => p.Value)
                    .Where(p => !string.IsNullOrEmpty(p.SiteUrl) && p.ClosedDate.HasValue)
                    .Where(p => onlyProject == null || p.Number == onlyProject)
                    .ToList();

                foreach (var project in closed)
                {
                    var decisions = ProcessSite(project, today, whatIf);
                    result.Decisions.AddRange(decisions);
                    result.SitesProcessed++;

                    if (!whatIf)
                    {
                        foreach (var d in decisions.Where(x => x.Action != RetentionAction.Keep))
                        {
                            hub.Log(ListNames.RetentionLog, project.Number + " " + d.Action, new FieldValues()
                                .Set(FieldNames.ProjectNumber, project.Number)
                                .Set(FieldNames.Action, d.Action.ToString())
                                .Set(FieldNames.ItemUrl, d.ItemUrl)
                                .Set(FieldNames.RetentionCategory, d.Category)
                                .Set(FieldNames.Reason, d.Reason));
                        }

                        hubSession.Commit();
                    }
                }
            }

            _log.Info(string.Format("Retention ({0}): {1} sites, {2} records declared, {3} archived, {4} deleted",
                whatIf ? "preview" : "applied", result.SitesProcessed, result.Count(RetentionAction.DeclareRecord),
                result.Count(RetentionAction.MoveToArchive), result.Count(RetentionAction.Delete)));
            return result;
        }

        private List<RetentionDecision> ProcessSite(Project project, DateTime today, bool whatIf)
        {
            var decisions = new List<RetentionDecision>();
            using (var site = _connector.Open(project.SiteUrl))
            {
                foreach (var discipline in ProjectSiteSchema.DisciplineLibraries(Discipline.All.Select(d => d.Code)))
                {
                    if (!site.ListExists(discipline.LibraryName))
                    {
                        continue;
                    }

                    var library = site.GetList(discipline.LibraryName);
                    foreach (var item in library.Query(CamlQuery.All(200)).ToList())
                    {
                        var category = item.GetString(FieldNames.RetentionCategory);
                        if (string.IsNullOrEmpty(category))
                        {
                            category = RetentionPolicy.CategoryFor(
                                item.GetString(FieldNames.DocumentType),
                                item.GetEnum(FieldNames.DeliverableStatus, DeliverableStatus.WorkInProgress));
                        }

                        var decision = _policy.Decide(item.GetString(FieldNames.FileRef), category, item.GetBool(FieldNames.IsRecord), project.ClosedDate, today);
                        decisions.Add(decision);

                        if (whatIf || decision.Action == RetentionAction.Keep)
                        {
                            continue;
                        }

                        try
                        {
                            Apply(library, item, decision);
                        }
                        catch (Exception ex)
                        {
                            decision.Reason = "Failed: " + ex.Message;
                            _log.Error("Retention action failed for " + decision.ItemUrl, ex);
                        }
                    }
                }

                site.Commit();
            }

            return decisions;
        }

        private static void Apply(IListGateway library, ListItemData item, RetentionDecision decision)
        {
            switch (decision.Action)
            {
                case RetentionAction.DeclareRecord:
                    library.Update(item.Id, new FieldValues()
                        .Set(FieldNames.IsRecord, true)
                        .Set(FieldNames.RetentionCategory, decision.Category), true);
                    break;
                case RetentionAction.MoveToArchive:
                    decision.ItemUrl = library.MoveFile(item.Id, ProjectSiteSchema.ArchiveLibrary);
                    break;
                case RetentionAction.Delete:
                    library.Recycle(item.Id);
                    break;
            }
        }
    }
}
