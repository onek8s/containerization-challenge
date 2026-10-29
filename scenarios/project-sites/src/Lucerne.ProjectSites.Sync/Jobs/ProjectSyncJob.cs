using System;
using System.Globalization;
using System.Linq;
using AutoMapper;
using Lucerne.ProjectSites.Contracts.Schema;
using Lucerne.ProjectSites.Core;
using Lucerne.ProjectSites.Core.Mapping;
using Lucerne.ProjectSites.Core.Services;
using Lucerne.ProjectSites.Domain;
using Lucerne.ProjectSites.Integration.Erp;
using Lucerne.ProjectSites.SharePoint;

namespace Lucerne.ProjectSites.Sync.Jobs
{
    /// <summary>
    /// ERP projects -> hub "Projects" list. New active projects get a site request; stage changes are pushed
    /// to the project site's property bag (the retention job keys off the closed date).
    /// </summary>
    public class ProjectSyncJob : ISyncJob
    {
        public const string WatermarkName = "erp.projects";

        private readonly IErpProjectSource _erp;
        private readonly ISiteConnector _connector;
        private readonly IMapper _mapper;
        private readonly ISyncStateStore _state;
        private readonly SyncOptions _options;
        private readonly IActivityLog _log;

        public ProjectSyncJob(IErpProjectSource erp, ISiteConnector connector, IMapper mapper, ISyncStateStore state, SyncOptions options, IActivityLog log)
        {
            _erp = erp;
            _connector = connector;
            _mapper = mapper;
            _state = state;
            _options = options;
            _log = log ?? NullActivityLog.Instance;
        }

        public string Name
        {
            get { return "ProjectSync"; }
        }

        public SyncRun Run(DateTime nowUtc)
        {
            var run = new SyncRun();
            var since = (_state.GetWatermark(WatermarkName) ?? new DateTime(2014, 1, 1, 0, 0, 0, DateTimeKind.Utc)) - _options.WatermarkOverlap;
            var maxSeen = since;

            using (var hubSession = _connector.Open(_options.HubUrl))
            {
                var hub = new HubRepository(hubSession);
                var staff = hub.GetStaffByEmployeeId();
                var projects = hubSession.GetList(ListNames.Projects);
                var requests = hubSession.GetList(ListNames.SiteRequests);

                foreach (var erp in _erp.GetChangedProjects(since))
                {
                    try
                    {
                        var project = _mapper.Map<Project>(erp);
                        project.ProjectManagerLogin = Login(staff, erp.ManagerEmployeeNo);
                        project.ProjectDirectorLogin = Login(staff, erp.DirectorEmployeeNo);

                        var existing = hub.FindProject(project.Number);
                        var fields = ListMapper.ToFields(project);
                        if (existing == null)
                        {
                            projects.Add(fields);
                            run.Created++;
                        }
                        else
                        {
                            var before = existing.Value.Value;
                            project.SiteUrl = before.SiteUrl;
                            projects.Update(existing.Value.Key, fields, true);
                            run.Updated++;
                            if (before.Stage != project.Stage && !string.IsNullOrEmpty(project.SiteUrl))
                            {
                                PushStage(project);
                            }
                        }

                        if (_options.CreateSiteRequests && project.NeedsSite && project.Stage == ProjectStage.Active && !HasOpenRequest(requests, project.Number))
                        {
                            requests.Add(new FieldValues()
                                .Set(FieldNames.Title, "Site for " + project.Number)
                                .Set(FieldNames.ProjectNumber, project.Number)
                                .Set(FieldNames.RequestStatus, SiteRequestStatus.Queued.ToString())
                                .Set(FieldNames.Requester, new UserValue(_options.ServiceLogin)));
                            _log.Info("Site requested for " + project.Number);
                        }

                        if (erp.LastChangedUtc > maxSeen)
                        {
                            maxSeen = erp.LastChangedUtc;
                        }
                    }
                    catch (Exception ex)
                    {
                        run.Failed++;
                        _log.Error("Project " + erp.ProjectNo + " could not be synchronised", ex);
                    }
                }

                hubSession.Commit();
            }

            // Only advance when everything went through, so failed records are read again next time.
            if (run.Failed == 0 && maxSeen > since)
            {
                _state.SetWatermark(WatermarkName, maxSeen);
            }

            run.Message = "since " + since.ToString("u", CultureInfo.InvariantCulture);
            return run;
        }

        private void PushStage(Project project)
        {
            using (var site = _connector.Open(project.SiteUrl))
            {
                site.SetProperty(WebProperties.ProjectStage, project.Stage.ToString());
                if (project.ClosedDate.HasValue)
                {
                    site.SetProperty(WebProperties.ClosedDate, project.ClosedDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                }

                site.Commit();
            }

            _log.Info(project.Number + " is now " + project.Stage);
        }

        private static bool HasOpenRequest(IListGateway requests, string number)
        {
            return requests.Query(new CamlQuery
            {
                Query = Caml.Where(Caml.And(
                    Caml.Eq(FieldNames.ProjectNumber, "Text", number),
                    Caml.In(FieldNames.RequestStatus, "Choice", "New", "Queued", "Provisioning"))),
                RowLimit = 1,
                MaxItems = 1
            }).Any();
        }

        private static string Login(System.Collections.Generic.IDictionary<string, StaffMember> staff, string employeeNo)
        {
            StaffMember member;
            return !string.IsNullOrEmpty(employeeNo) && staff.TryGetValue(employeeNo, out member) && !string.IsNullOrEmpty(member.AccountName)
                ? member.ClaimsLogin
                : null;
        }
    }
}
