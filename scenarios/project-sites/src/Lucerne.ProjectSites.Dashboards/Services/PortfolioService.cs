using System;
using System.Collections.Generic;
using System.Linq;
using Lucerne.ProjectSites.Contracts.Schema;
using Lucerne.ProjectSites.Core;
using Lucerne.ProjectSites.Core.Mapping;
using Lucerne.ProjectSites.Core.Services;
using Lucerne.ProjectSites.Dashboards.Infrastructure;
using Lucerne.ProjectSites.Dashboards.Models;
using Lucerne.ProjectSites.Documents;
using Lucerne.ProjectSites.Domain;
using Lucerne.ProjectSites.SharePoint;
using Lucerne.ProjectSites.SharePoint.Auth;
using Lucerne.ProjectSites.SharePoint.Csom;

namespace Lucerne.ProjectSites.Dashboards.Services
{
    /// <summary>
    /// Reads the hub with an app-only token (portfolio figures are visible to everyone) and project sites with
    /// a user + app token, so a user only sees deliverables of sites they have access to.
    /// </summary>
    public class PortfolioService
    {
        private readonly AddInSettings _settings;
        private readonly HighTrustTokenIssuer _issuer;
        private readonly IActivityLog _log;

        public PortfolioService(AddInSettings settings, HighTrustTokenIssuer issuer, IActivityLog log)
        {
            _settings = settings;
            _issuer = issuer;
            _log = log;
        }

        public ISiteConnector AppOnly()
        {
            return new CsomSiteConnector(new HighTrustAppOnlyContextFactory(_issuer));
        }

        public ISiteConnector ForUser(string userSid)
        {
            return new CsomSiteConnector(new HighTrustUserContextFactory(_issuer, userSid));
        }

        public PortfolioViewModel GetPortfolio(string office, bool exceptionsOnly)
        {
            using (var hub = AppOnly().Open(_settings.HubUrl))
            {
                var repo = new HubRepository(hub);
                var projects = repo.GetProjects(ProjectStage.Active, ProjectStage.OnHold, ProjectStage.Closing).Select(p => p.Value).ToList();
                var statuses = hub.GetList(ListNames.ProjectStatus).Query(CamlQuery.All())
                    .Select(ListMapper.ToStatus)
                    .GroupBy(s => s.ProjectNumber)
                    .ToDictionary(g => g.Key, g => g.First());

                var rows = projects
                    .Where(p => string.IsNullOrEmpty(office) || p.OfficeCode == office)
                    .Select(p =>
                    {
                        ProjectStatus s;
                        statuses.TryGetValue(p.Number, out s);
                        return new PortfolioRow { Project = p, Status = s };
                    })
                    .Where(r => !exceptionsOnly || (r.Status != null && r.Status.Overall >= RagStatus.Amber))
                    .OrderBy(r => r.Project.Number)
                    .ToList();

                return new PortfolioViewModel
                {
                    Office = office,
                    ExceptionsOnly = exceptionsOnly,
                    Rows = rows,
                    Offices = Office.All.Select(o => o.Code).ToList(),
                    Counts = rows.GroupBy(r => r.Status == null ? RagStatus.NotAssessed : r.Status.Overall).ToDictionary(g => g.Key, g => g.Count())
                };
            }
        }

        public ProjectViewModel GetProject(string number, string userSid)
        {
            Project project;
            ProjectStatus status = null;
            using (var hub = AppOnly().Open(_settings.HubUrl))
            {
                var repo = new HubRepository(hub);
                var entry = repo.FindProject(number);
                if (entry == null)
                {
                    return null;
                }

                project = entry.Value.Value;
                var statusItem = repo.FindByText(ListNames.ProjectStatus, FieldNames.ProjectNumber, number);
                if (statusItem != null)
                {
                    status = ListMapper.ToStatus(statusItem);
                }
            }

            var model = new ProjectViewModel { Project = project, Status = status };
            if (string.IsNullOrEmpty(project.SiteUrl))
            {
                return model;
            }

            try
            {
                var snapshot = new StatusRollupService(ForUser(userSid), _settings.HubUrl, null, _log).ReadSnapshot(project);
                model.Deliverables = snapshot.Deliverables;
                model.Risks = snapshot.Risks.Where(r => r.Open).OrderByDescending(r => r.Severity).ToList();
                model.CanSeeSite = true;
            }
            catch (Exception ex)
            {
                // Access denied for users outside the project team; the hub figures are still shown.
                _log.Warn("Project site " + project.SiteUrl + " not readable for " + userSid + ": " + ex.Message);
            }

            return model;
        }
    }
}
