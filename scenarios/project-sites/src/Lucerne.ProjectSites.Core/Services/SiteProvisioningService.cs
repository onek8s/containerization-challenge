using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Lucerne.ProjectSites.Contracts.Schema;
using Lucerne.ProjectSites.Domain;
using Lucerne.ProjectSites.SharePoint;

namespace Lucerne.ProjectSites.Core.Services
{
    public class ProvisioningPlan
    {
        public string ProjectNumber { get; set; }
        public SiteCreationRequest Site { get; set; }
        public List<string> DisciplineLibraries { get; set; }
        public string OwnerLogin { get; set; }
        public string DirectorLogin { get; set; }
        public bool Confidential { get; set; }
    }

    public class ProvisioningResult
    {
        public ProvisioningResult()
        {
            Created = new List<string>();
            Failed = new Dictionary<int, string>();
        }

        public List<string> Created { get; private set; }
        public Dictionary<int, string> Failed { get; private set; }
        public int Rejected { get; set; }
    }

    /// <summary>
    /// Works through the hub "Site Requests" queue: creates a site collection per project from the Lucerne
    /// project web template, sets the property bag and groups, and links the site in the project register.
    /// Requests come from the sync service (new ERP projects) and from PMs (bids).
    /// </summary>
    public class SiteProvisioningService
    {
        private readonly ISiteConnector _connector;
        private readonly ISiteProvisioner _provisioner;
        private readonly string _hubUrl;
        private readonly string _webApplicationUrl;
        private readonly IActivityLog _log;

        public SiteProvisioningService(ISiteConnector connector, ISiteProvisioner provisioner, string hubUrl, string webApplicationUrl, IActivityLog log)
        {
            _connector = connector;
            _provisioner = provisioner;
            _hubUrl = hubUrl;
            _webApplicationUrl = webApplicationUrl.TrimEnd('/');
            _log = log ?? NullActivityLog.Instance;
            Lcid = 1033;
            ContentDatabasePrefix = "WSS_Content_Projects";
            MaxRequestsPerRun = 10;
        }

        public int Lcid { get; set; }
        public string ContentDatabasePrefix { get; set; }
        public int MaxRequestsPerRun { get; set; }

        public ProvisioningPlan Plan(Project project, string requesterLogin)
        {
            var url = _webApplicationUrl + Project.SiteRelativeUrl(project.Number);
            var owner = project.ProjectManagerLogin ?? requesterLogin;
            var secondary = project.ProjectDirectorLogin ?? requesterLogin;
            var year = project.Number.Substring(1, 2);

            return new ProvisioningPlan
            {
                ProjectNumber = project.Number,
                OwnerLogin = owner,
                DirectorLogin = project.ProjectDirectorLogin,
                Confidential = project.Confidential,
                DisciplineLibraries = ProjectSiteSchema.DisciplineLibraries(project.Disciplines).Select(d => d.LibraryName).ToList(),
                Site = new SiteCreationRequest
                {
                    Url = url,
                    Title = project.Number + " " + project.Name,
                    Description = project.ClientName,
                    WebTemplate = TemplateNames.ProjectWebTemplate,
                    Lcid = Lcid,
                    OwnerLogin = owner,
                    SecondaryOwnerLogin = secondary,
                    // One content database per project year keeps databases under the 200 GB guidance.
                    ContentDatabase = ContentDatabasePrefix + "_20" + year,
                    Properties = new Dictionary<string, string>
                    {
                        { WebProperties.ProjectNumber, project.Number },
                        { WebProperties.ProjectStage, project.Stage.ToString() },
                        { WebProperties.Office, project.OfficeCode },
                        { WebProperties.HubUrl, _hubUrl },
                        { WebProperties.TemplateVersion, TemplateNames.CurrentVersion.ToString(CultureInfo.InvariantCulture) },
                        { WebProperties.ProvisionedUtc, DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) },
                        { "lucerne_disciplines", string.Join(";", project.Disciplines) }
                    }
                }
            };
        }

        public ProvisioningResult ProcessQueue()
        {
            var result = new ProvisioningResult();
            using (var hubSession = _connector.Open(_hubUrl))
            {
                var hub = new HubRepository(hubSession);
                var requests = hubSession.GetList(ListNames.SiteRequests);
                var queued = requests.Query(new CamlQuery
                {
                    Query = Caml.Where(Caml.In(FieldNames.RequestStatus, "Choice", SiteRequestStatus.New.ToString(), SiteRequestStatus.Queued.ToString()))
                        + Caml.OrderBy(FieldNames.Id),
                    RowLimit = MaxRequestsPerRun,
                    MaxItems = MaxRequestsPerRun
                }).ToList();

                foreach (var request in queued)
                {
                    var number = request.GetString(FieldNames.ProjectNumber);
                    var entry = Project.IsValidNumber(number) ? hub.FindProject(number) : null;
                    if (entry == null)
                    {
                        requests.Update(request.Id, Status(SiteRequestStatus.Rejected, "Project " + number + " is not in the project register."), true);
                        result.Rejected++;
                        continue;
                    }

                    var project = entry.Value.Value;
                    if (!string.IsNullOrEmpty(project.SiteUrl))
                    {
                        requests.Update(request.Id, Status(SiteRequestStatus.Completed, "Site already exists: " + project.SiteUrl), true);
                        continue;
                    }

                    requests.Update(request.Id, Status(SiteRequestStatus.Provisioning, null), true);
                    hubSession.Commit();

                    try
                    {
                        var plan = Plan(project, request.GetUserLogin(FieldNames.Requester));
                        Execute(plan);
                        hubSession.GetList(ListNames.Projects).Update(entry.Value.Key, new FieldValues().Set(FieldNames.SiteUrl, plan.Site.Url), true);
                        requests.Update(request.Id, Status(SiteRequestStatus.Completed, "Created " + plan.Site.Url).Set(FieldNames.SiteUrl, plan.Site.Url), true);
                        result.Created.Add(plan.Site.Url);
                        _log.Info("Provisioned " + plan.Site.Url);
                    }
                    catch (Exception ex)
                    {
                        requests.Update(request.Id, Status(SiteRequestStatus.Failed, ex.Message), true);
                        result.Failed[request.Id] = ex.Message;
                        _log.Error("Provisioning failed for " + number, ex);
                    }

                    hubSession.Commit();
                }
            }

            return result;
        }

        public void Execute(ProvisioningPlan plan)
        {
            if (!_provisioner.SiteExists(plan.Site.Url))
            {
                _provisioner.CreateSiteCollection(plan.Site);
            }

            using (var site = _connector.Open(plan.Site.Url))
            {
                foreach (var p in plan.Site.Properties)
                {
                    site.SetProperty(p.Key, p.Value);
                }

                var security = site.Security;
                security.EnsureGroup(ProjectSiteSchema.OwnersGroup(plan.ProjectNumber), "Full Control", "Project manager and leads");
                security.EnsureGroup(ProjectSiteSchema.MembersGroup(plan.ProjectNumber), "Lucerne Contribute", "Project team (maintained from timesheets)");
                security.EnsureGroup(ProjectSiteSchema.VisitorsGroup(plan.ProjectNumber), "Read", plan.Confidential ? "Named readers only" : "All staff");
                security.AddToGroup(ProjectSiteSchema.OwnersGroup(plan.ProjectNumber), plan.OwnerLogin);
                if (!string.IsNullOrEmpty(plan.DirectorLogin))
                {
                    security.AddToGroup(ProjectSiteSchema.OwnersGroup(plan.ProjectNumber), plan.DirectorLogin);
                }

                if (!plan.Confidential)
                {
                    // Claim from the Lucerne claims provider: every employee.
                    security.AddToGroup(ProjectSiteSchema.VisitorsGroup(plan.ProjectNumber), "c:0-.t|lucerne|allstaff");
                }

                site.Commit();
            }
        }

        private static FieldValues Status(SiteRequestStatus status, string message)
        {
            return new FieldValues()
                .Set(FieldNames.RequestStatus, status.ToString())
                .Set(FieldNames.RequestMessage, message);
        }
    }
}
