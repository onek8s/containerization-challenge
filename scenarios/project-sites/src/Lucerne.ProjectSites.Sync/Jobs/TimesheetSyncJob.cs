using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Lucerne.ProjectSites.Contracts.Schema;
using Lucerne.ProjectSites.Core;
using Lucerne.ProjectSites.Core.Services;
using Lucerne.ProjectSites.Domain;
using Lucerne.ProjectSites.Integration.Erp;
using Lucerne.ProjectSites.SharePoint;

namespace Lucerne.ProjectSites.Sync.Jobs
{
    /// <summary>
    /// Approved ERP timesheet lines -> "Project Hours" list of each project site, one row per employee and ISO
    /// week. The last TimesheetWeeks weeks are rewritten on every run; rows that disappeared are set to zero.
    /// </summary>
    public class TimesheetSyncJob : ISyncJob
    {
        private readonly IErpProjectSource _erp;
        private readonly ISiteConnector _connector;
        private readonly SyncOptions _options;
        private readonly IActivityLog _log;

        public TimesheetSyncJob(IErpProjectSource erp, ISiteConnector connector, SyncOptions options, IActivityLog log)
        {
            _erp = erp;
            _connector = connector;
            _options = options;
            _log = log ?? NullActivityLog.Instance;
        }

        public string Name
        {
            get { return "TimesheetSync"; }
        }

        public static string RowKey(string employeeId, IsoWeek week)
        {
            return employeeId + "|" + week;
        }

        public SyncRun Run(DateTime nowUtc)
        {
            var run = new SyncRun();
            var firstWeek = IsoWeek.FromDate(nowUtc).AddWeeks(-(_options.TimesheetWeeks - 1));
            var from = firstWeek.Monday;
            var to = nowUtc.Date;

            List<Project> projects;
            IDictionary<string, StaffMember> staff;
            using (var hubSession = _connector.Open(_options.HubUrl))
            {
                var hub = new HubRepository(hubSession);
                staff = hub.GetStaffByEmployeeId();
                projects = hub.GetProjects(ProjectStage.Active, ProjectStage.OnHold, ProjectStage.Closing)
                    .Select(p => p.Value)
                    .Where(p => !string.IsNullOrEmpty(p.SiteUrl))
                    .ToList();
            }

            foreach (var project in projects)
            {
                try
                {
                    var lines = _erp.GetApprovedTimesheetLines(from, to, project.Number);
                    var totals = lines
                        .Where(l => string.Equals(l.ApprovalState, "Approved", StringComparison.OrdinalIgnoreCase))
                        .GroupBy(l => RowKey(l.EmployeeNo, IsoWeek.FromDate(l.WorkDate)))
                        .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));

                    using (var site = _connector.Open(project.SiteUrl))
                    {
                        var hours = site.GetList(ListNames.Hours);
                        var rows = hours.Query(new CamlQuery
                        {
                            Query = Caml.Where(Caml.Geq(FieldNames.Week, "Text", firstWeek.ToString())),
                            RowLimit = 1000
                        }).ToDictionary(r => r.GetString(FieldNames.Title) ?? string.Empty, r => r);

                        foreach (var total in totals)
                        {
                            var parts = total.Key.Split('|');
                            StaffMember member;
                            staff.TryGetValue(parts[0], out member);
                            var values = new FieldValues()
                                .Set(FieldNames.Title, total.Key)
                                .Set(FieldNames.EmployeeId, parts[0])
                                .Set(FieldNames.AccountName, member == null ? null : new UserValue(member.ClaimsLogin, member.DisplayName))
                                .Set(FieldNames.Discipline, member?.DisciplineCode)
                                .Set(FieldNames.Office, member?.OfficeCode)
                                .Set(FieldNames.Week, parts[1])
                                .Set(FieldNames.Hours, (double)total.Value);

                            ListItemData row;
                            if (rows.TryGetValue(total.Key, out row))
                            {
                                if (row.GetDecimal(FieldNames.Hours) != total.Value)
                                {
                                    hours.Update(row.Id, values, true);
                                    run.Updated++;
                                }
                            }
                            else
                            {
                                hours.Add(values);
                                run.Created++;
                            }
                        }

                        foreach (var stale in rows.Where(r => !totals.ContainsKey(r.Key) && r.Value.GetDecimal(FieldNames.Hours) != 0))
                        {
                            hours.Update(stale.Value.Id, new FieldValues().Set(FieldNames.Hours, 0d), true);
                            run.Updated++;
                        }

                        site.Commit();
                    }
                }
                catch (Exception ex)
                {
                    run.Failed++;
                    _log.Error("Timesheets for " + project.Number + " could not be synchronised", ex);
                }
            }

            run.Message = projects.Count + " projects, weeks " + firstWeek + " to " + IsoWeek.FromDate(to).ToString();
            return run;
        }
    }
}
