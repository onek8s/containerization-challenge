using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Lucerne.ProjectSites.Contracts.DocumentControl;
using Lucerne.ProjectSites.Contracts.Schema;
using Lucerne.ProjectSites.Core;
using Lucerne.ProjectSites.Core.Services;
using Lucerne.ProjectSites.Domain;
using Lucerne.ProjectSites.Integration.DocumentControl;
using Lucerne.ProjectSites.SharePoint;

namespace Lucerne.ProjectSites.Sync.Jobs
{
    /// <summary>
    /// Approved documents in the discipline libraries are submitted to the document-control system; the
    /// registration result is written back (Registered = issued, Rejected = back to the author).
    /// </summary>
    public class ApprovalPublishJob : ISyncJob
    {
        private readonly IDocumentControlGateway _docControl;
        private readonly ISiteConnector _connector;
        private readonly SyncOptions _options;
        private readonly IActivityLog _log;

        public ApprovalPublishJob(IDocumentControlGateway docControl, ISiteConnector connector, SyncOptions options, IActivityLog log)
        {
            _docControl = docControl;
            _connector = connector;
            _options = options;
            _log = log ?? NullActivityLog.Instance;
        }

        public string Name
        {
            get { return "ApprovalPublish"; }
        }

        public SyncRun Run(DateTime nowUtc)
        {
            var run = new SyncRun();
            List<Project> projects;
            using (var hubSession = _connector.Open(_options.HubUrl))
            {
                projects = new HubRepository(hubSession).GetProjects(ProjectStage.Active, ProjectStage.Closing)
                    .Select(p => p.Value)
                    .Where(p => !string.IsNullOrEmpty(p.SiteUrl))
                    .ToList();
            }

            foreach (var project in projects)
            {
                try
                {
                    using (var site = _connector.Open(project.SiteUrl))
                    {
                        foreach (var discipline in ProjectSiteSchema.DisciplineLibraries(project.Disciplines))
                        {
                            if (!site.ListExists(discipline.LibraryName))
                            {
                                continue;
                            }

                            var library = site.GetList(discipline.LibraryName);
                            Submit(project, library, run);
                            Poll(library, nowUtc, run);
                        }

                        site.Commit();
                    }
                }
                catch (Exception ex)
                {
                    run.Failed++;
                    _log.Error("Approval publishing failed for " + project.Number, ex);
                }
            }

            return run;
        }

        private void Submit(Project project, IListGateway library, SyncRun run)
        {
            var approved = library.Query(new CamlQuery
            {
                Query = Caml.Where(Caml.And(
                    Caml.Eq(FieldNames.ApprovalStatus, "Choice", ApprovalStatus.Approved.ToString()),
                    Caml.IsNull(FieldNames.DocControlId))),
                RowLimit = 100,
                Recursive = true
            }).ToList();

            foreach (var item in approved)
            {
                var number = item.GetString(FieldNames.DocumentNumber);
                try
                {
                    var content = library.OpenFile(item.Id);
                    var submission = new DocumentSubmission
                    {
                        ProjectNumber = project.Number,
                        DocumentNumber = number,
                        Title = item.GetString(FieldNames.Title),
                        Revision = item.GetString(FieldNames.Revision),
                        Discipline = item.GetString(FieldNames.Discipline),
                        DocumentType = item.GetString(FieldNames.DocumentType),
                        ApprovedBy = Claims.AccountName(item.GetUserLogin(FieldNames.ApprovedBy)),
                        ApprovedOn = item.GetDate(FieldNames.ApprovedDate),
                        SourceUrl = item.GetString(FieldNames.FileRef),
                        FileName = item.GetString(FieldNames.FileLeafRef),
                        Sha256 = Sha256(content)
                    };

                    var receipt = _docControl.Submit(submission, content);
                    library.Update(item.Id, new FieldValues()
                        .Set(FieldNames.DocControlId, receipt.SubmissionId)
                        .Set(FieldNames.DocControlStatus, receipt.Status), true);
                    run.Created++;
                    _log.Info("Submitted " + number + " rev " + submission.Revision + " as " + receipt.SubmissionId);
                }
                catch (Exception ex)
                {
                    run.Failed++;
                    _log.Error("Submission of " + number + " failed", ex);
                }
            }
        }

        private void Poll(IListGateway library, DateTime nowUtc, SyncRun run)
        {
            var pending = library.Query(new CamlQuery
            {
                Query = Caml.Where(Caml.In(FieldNames.DocControlStatus, "Text", SubmissionStates.Received, SubmissionStates.Checking)),
                RowLimit = 100,
                Recursive = true
            }).ToList();

            foreach (var item in pending)
            {
                var status = _docControl.GetStatus(item.GetString(FieldNames.DocControlId));
                if (status == null || status.Status == item.GetString(FieldNames.DocControlStatus))
                {
                    continue;
                }

                var values = new FieldValues().Set(FieldNames.DocControlStatus, status.Status);
                if (status.Status == SubmissionStates.Registered)
                {
                    values.Set(FieldNames.DeliverableStatus, DeliverableStatus.Issued.ToString())
                        .Set(FieldNames.IssuedDate, nowUtc.Date);
                }
                else if (status.Status == SubmissionStates.Rejected)
                {
                    values.Set(FieldNames.ApprovalStatus, ApprovalStatus.Rejected.ToString())
                        .Set(FieldNames.DocControlId, null)
                        .Set(FieldNames.Reason, status.Comment);
                }

                library.Update(item.Id, values, true);
                run.Updated++;
            }
        }

        private static string Sha256(byte[] content)
        {
            using (var sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(content)).Replace("-", string.Empty).ToLowerInvariant();
            }
        }
    }
}
