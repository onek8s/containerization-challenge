using System;
using System.Collections.Generic;
using System.Linq;
using Lucerne.ProjectSites.Contracts.Schema;
using Lucerne.ProjectSites.Domain;
using Lucerne.ProjectSites.SharePoint;

namespace Lucerne.ProjectSites.Core.Services
{
    public class ResourceSyncResult
    {
        public int SitesProcessed { get; set; }
        public int MembersAdded { get; set; }
        public int MembersRemoved { get; set; }
        public int TeamRowsAdded { get; set; }
        public int TeamRowsUpdated { get; set; }
    }

    /// <summary>
    /// Keeps the "Project Team" list and the members group of each project site in line with who books
    /// hours on the project. Staff who booked in the last eight weeks are added; members that were added by
    /// this job and stopped booking (or left the company) are removed again. Manual team entries are kept.
    /// </summary>
    public class ResourceSyncService
    {
        public const string BookedRole = "Booked hours";
        public const string ManagerRole = "Project Manager";
        public const string DirectorRole = "Project Director";

        private readonly ISiteConnector _connector;
        private readonly string _hubUrl;
        private readonly IActivityLog _log;

        public ResourceSyncService(ISiteConnector connector, string hubUrl, IActivityLog log)
        {
            _connector = connector;
            _hubUrl = hubUrl;
            _log = log ?? NullActivityLog.Instance;
            ActiveWeeks = 8;
        }

        public int ActiveWeeks { get; set; }

        public ResourceSyncResult Run(DateTime today)
        {
            var result = new ResourceSyncResult();
            using (var hubSession = _connector.Open(_hubUrl))
            {
                var hub = new HubRepository(hubSession);
                var staff = hub.GetStaffByEmployeeId();
                var byLogin = staff.Values.Where(s => !string.IsNullOrEmpty(s.AccountName))
                    .GroupBy(s => s.ClaimsLogin, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
                var projects = hub.GetProjects(ProjectStage.Active, ProjectStage.OnHold, ProjectStage.Closing)
                    .Select(p => p.Value)
                    .Where(p => !string.IsNullOrEmpty(p.SiteUrl));

                var cutoff = IsoWeek.FromDate(today).AddWeeks(-ActiveWeeks);
                foreach (var project in projects)
                {
                    try
                    {
                        SyncSite(project, staff, byLogin, cutoff, result);
                        result.SitesProcessed++;
                    }
                    catch (Exception ex)
                    {
                        _log.Error("Resource sync failed for " + project.Number, ex);
                    }
                }
            }

            _log.Info(string.Format("Resource sync: {0} sites, +{1} / -{2} members", result.SitesProcessed, result.MembersAdded, result.MembersRemoved));
            return result;
        }

        private void SyncSite(Project project, IDictionary<string, StaffMember> staff, IDictionary<string, StaffMember> byLogin, IsoWeek cutoff, ResourceSyncResult result)
        {
            using (var site = _connector.Open(project.SiteUrl))
            {
                // Expected team from booked hours: employee id -> last booked week
                var lastBooked = new Dictionary<string, IsoWeek>();
                foreach (var row in site.GetList(ListNames.Hours).Query(CamlQuery.All()))
                {
                    var emp = row.GetString(FieldNames.EmployeeId);
                    var weekText = row.GetString(FieldNames.Week);
                    if (string.IsNullOrEmpty(emp) || string.IsNullOrEmpty(weekText) || row.GetDecimal(FieldNames.Hours) <= 0)
                    {
                        continue;
                    }

                    var week = IsoWeek.Parse(weekText);
                    IsoWeek current;
                    if (!lastBooked.TryGetValue(emp, out current) || week.CompareTo(current) > 0)
                    {
                        lastBooked[emp] = week;
                    }
                }

                var expected = new Dictionary<string, KeyValuePair<StaffMember, string>>(StringComparer.OrdinalIgnoreCase);
                foreach (var pair in lastBooked.Where(p => p.Value.CompareTo(cutoff) >= 0))
                {
                    StaffMember member;
                    if (staff.TryGetValue(pair.Key, out member) && member.Active)
                    {
                        expected[member.ClaimsLogin] = new KeyValuePair<StaffMember, string>(member, BookedRole);
                    }
                }

                AddLead(expected, byLogin, project.ProjectManagerLogin, ManagerRole);
                AddLead(expected, byLogin, project.ProjectDirectorLogin, DirectorRole);

                var team = site.GetList(ListNames.Team);
                var rows = team.Query(CamlQuery.All()).ToList();
                var membersGroup = ProjectSiteSchema.MembersGroup(project.Number);
                var groupMembers = new HashSet<string>(site.Security.GetGroupMembers(membersGroup), StringComparer.OrdinalIgnoreCase);

                foreach (var e in expected)
                {
                    var row = rows.FirstOrDefault(r => string.Equals(r.GetUserLogin(FieldNames.AccountName), e.Key, StringComparison.OrdinalIgnoreCase));
                    var member = e.Value.Key;
                    IsoWeek last;
                    var values = new FieldValues()
                        .Set(FieldNames.Title, member.DisplayName)
                        .Set(FieldNames.AccountName, new UserValue(e.Key, member.DisplayName))
                        .Set(FieldNames.EmployeeId, member.EmployeeId)
                        .Set(FieldNames.Discipline, member.DisciplineCode)
                        .Set(FieldNames.Office, member.OfficeCode)
                        .Set(FieldNames.Active, true)
                        .Set(FieldNames.LastBooked, lastBooked.TryGetValue(member.EmployeeId, out last) ? last.ToString() : null);

                    if (row == null)
                    {
                        values.Set(FieldNames.TeamRole, e.Value.Value);
                        team.Add(values);
                        result.TeamRowsAdded++;
                    }
                    else
                    {
                        team.Update(row.Id, values, true);
                        result.TeamRowsUpdated++;
                    }

                    if (!groupMembers.Contains(e.Key))
                    {
                        site.Security.AddToGroup(membersGroup, e.Key);
                        result.MembersAdded++;
                    }
                }

                // Remove only what this job added: rows with the booked role that are no longer expected.
                foreach (var row in rows.Where(r => r.GetString(FieldNames.TeamRole) == BookedRole && r.GetBool(FieldNames.Active)))
                {
                    var login = row.GetUserLogin(FieldNames.AccountName);
                    if (login == null || expected.ContainsKey(login))
                    {
                        continue;
                    }

                    team.Update(row.Id, new FieldValues().Set(FieldNames.Active, false), true);
                    if (groupMembers.Contains(login))
                    {
                        site.Security.RemoveFromGroup(membersGroup, login);
                        result.MembersRemoved++;
                    }
                }

                site.Commit();
            }
        }

        private static void AddLead(IDictionary<string, KeyValuePair<StaffMember, string>> expected, IDictionary<string, StaffMember> byLogin, string login, string role)
        {
            StaffMember member;
            if (!string.IsNullOrEmpty(login) && byLogin.TryGetValue(Claims.WindowsLogin(login), out member) && member.Active)
            {
                expected[member.ClaimsLogin] = new KeyValuePair<StaffMember, string>(member, role);
            }
        }
    }
}
