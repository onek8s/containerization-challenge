using System;
using System.Globalization;
using System.Linq;
using Lucerne.ProjectSites.Contracts.Schema;
using Lucerne.ProjectSites.Domain;
using Lucerne.ProjectSites.SharePoint;

namespace Lucerne.ProjectSites.Core.Mapping
{
    /// <summary>Maps domain objects to list item field values and back.</summary>
    public static class ListMapper
    {
        public static FieldValues ToFields(Project p)
        {
            return new FieldValues()
                .Set(FieldNames.Title, p.Number + " " + p.Name)
                .Set(FieldNames.ProjectNumber, p.Number)
                .Set(FieldNames.ProjectName, p.Name)
                .Set(FieldNames.ClientName, p.ClientName)
                .Set(FieldNames.Office, p.OfficeCode)
                .Set(FieldNames.Sector, p.Sector)
                .Set(FieldNames.ProjectStage, p.Stage.ToString())
                .Set(FieldNames.ProjectManager, p.ProjectManagerLogin == null ? null : new UserValue(p.ProjectManagerLogin))
                .Set(FieldNames.ProjectDirector, p.ProjectDirectorLogin == null ? null : new UserValue(p.ProjectDirectorLogin))
                .Set(FieldNames.StartDate, p.StartDate)
                .Set(FieldNames.PlannedEndDate, p.PlannedEndDate)
                .Set(FieldNames.ClosedDate, p.ClosedDate)
                .Set(FieldNames.BudgetHours, (double)p.BudgetHours)
                .Set(FieldNames.FeeValue, (double)p.FeeValue)
                .Set(FieldNames.Currency, p.Currency)
                .Set(FieldNames.PercentComplete, (double)p.PercentComplete)
                .Set(FieldNames.Confidential, p.Confidential)
                .Set(FieldNames.Disciplines, p.Disciplines.ToArray())
                .Set(FieldNames.ErpModified, p.ErpModifiedUtc);
        }

        public static Project ToProject(ListItemData item)
        {
            var p = new Project
            {
                Number = item.GetString(FieldNames.ProjectNumber),
                Name = item.GetString(FieldNames.ProjectName),
                ClientName = item.GetString(FieldNames.ClientName),
                OfficeCode = item.GetString(FieldNames.Office),
                Sector = item.GetString(FieldNames.Sector),
                Stage = item.GetEnum(FieldNames.ProjectStage, ProjectStage.Active),
                ProjectManagerLogin = item.GetUserLogin(FieldNames.ProjectManager),
                ProjectDirectorLogin = item.GetUserLogin(FieldNames.ProjectDirector),
                StartDate = item.GetDate(FieldNames.StartDate),
                PlannedEndDate = item.GetDate(FieldNames.PlannedEndDate),
                ClosedDate = item.GetDate(FieldNames.ClosedDate),
                BudgetHours = item.GetDecimal(FieldNames.BudgetHours),
                FeeValue = item.GetDecimal(FieldNames.FeeValue),
                Currency = item.GetString(FieldNames.Currency),
                PercentComplete = item.GetDecimal(FieldNames.PercentComplete),
                Confidential = item.GetBool(FieldNames.Confidential),
                SiteUrl = item.GetString(FieldNames.SiteUrl),
                ErpModifiedUtc = item.GetDate(FieldNames.ErpModified) ?? DateTime.MinValue
            };
            p.Disciplines.AddRange(item.GetMulti(FieldNames.Disciplines));
            return p;
        }

        public static FieldValues ToFields(StaffMember s)
        {
            return new FieldValues()
                .Set(FieldNames.Title, s.DisplayName)
                .Set(FieldNames.EmployeeId, s.EmployeeId)
                .Set(FieldNames.AccountName, s.ClaimsLogin)
                .Set(FieldNames.Email, s.Email)
                .Set(FieldNames.Office, s.OfficeCode)
                .Set(FieldNames.Discipline, s.DisciplineCode)
                .Set(FieldNames.Grade, s.Grade)
                .Set(FieldNames.JobTitle, s.JobTitle)
                .Set(FieldNames.Active, s.Active);
        }

        public static StaffMember ToStaff(ListItemData item)
        {
            return new StaffMember
            {
                EmployeeId = item.GetString(FieldNames.EmployeeId),
                DisplayName = item.GetString(FieldNames.Title),
                AccountName = Claims.AccountName(item.GetString(FieldNames.AccountName)),
                Email = item.GetString(FieldNames.Email),
                OfficeCode = item.GetString(FieldNames.Office),
                DisciplineCode = item.GetString(FieldNames.Discipline),
                Grade = item.GetString(FieldNames.Grade),
                JobTitle = item.GetString(FieldNames.JobTitle),
                Active = item.GetBool(FieldNames.Active)
            };
        }

        public static Deliverable ToDeliverable(ListItemData item)
        {
            return new Deliverable
            {
                Id = item.Id,
                DocumentNumber = item.GetString(FieldNames.DocumentNumber),
                Title = item.GetString(FieldNames.Title),
                DisciplineCode = item.GetString(FieldNames.Discipline),
                DocumentType = item.GetString(FieldNames.DocumentType),
                Revision = item.GetString(FieldNames.Revision),
                Status = item.GetEnum(FieldNames.DeliverableStatus, DeliverableStatus.Planned),
                PlannedDate = item.GetDate(FieldNames.PlannedDate),
                ForecastDate = item.GetDate(FieldNames.ForecastDate),
                IssuedDate = item.GetDate(FieldNames.IssuedDate)
            };
        }

        public static Risk ToRisk(ListItemData item)
        {
            return new Risk
            {
                Title = item.GetString(FieldNames.Title),
                Severity = item.GetEnum(FieldNames.Severity, RiskSeverity.Low),
                Open = item.GetBool(FieldNames.IsOpen),
                OwnerLogin = item.GetUserLogin(FieldNames.Owner)
            };
        }

        public static Rfi ToRfi(ListItemData item)
        {
            return new Rfi
            {
                Reference = item.GetString(FieldNames.RfiReference),
                Subject = item.GetString(FieldNames.Title),
                Raised = item.GetDate(FieldNames.Raised) ?? DateTime.MinValue,
                ResponseDue = item.GetDate(FieldNames.ResponseDue),
                Closed = !item.GetBool(FieldNames.IsOpen)
            };
        }

        public static ActionItem ToAction(ListItemData item)
        {
            return new ActionItem
            {
                Title = item.GetString(FieldNames.Title),
                DueDate = item.GetDate(FieldNames.DueDate),
                Completed = item.GetBool(FieldNames.Completed),
                AssignedToLogin = item.GetUserLogin(FieldNames.AssignedTo)
            };
        }

        public static FieldValues ToFields(ProjectStatus s, string projectName, string office)
        {
            var metrics = string.Format(
                CultureInfo.InvariantCulture,
                "deliverables={0};issued={1};overdue={2};rfis={3};overdueRfis={4};highRisks={5};overdueActions={6};hours={7:0.#};budget={8:0.#};burn={9:0.00}",
                s.DeliverablesTotal, s.DeliverablesIssued, s.DeliverablesOverdue, s.OpenRfis, s.OverdueRfis, s.OpenHighRisks,
                s.OverdueActions, s.HoursToDate, s.BudgetHours, s.BurnRatio);

            return new FieldValues()
                .Set(FieldNames.Title, s.ProjectNumber + " " + projectName)
                .Set(FieldNames.ProjectNumber, s.ProjectNumber)
                .Set(FieldNames.Office, office)
                .Set(FieldNames.OverallStatus, s.Overall.ToString())
                .Set(FieldNames.ScheduleStatus, s.Schedule.ToString())
                .Set(FieldNames.BudgetStatus, s.Budget.ToString())
                .Set(FieldNames.RiskStatus, s.Risk.ToString())
                .Set(FieldNames.StatusSummary, s.Summary)
                .Set(FieldNames.StatusMetrics, metrics)
                .Set(FieldNames.AssessedDate, s.AssessedUtc);
        }

        public static ProjectStatus ToStatus(ListItemData item)
        {
            var s = new ProjectStatus
            {
                ProjectNumber = item.GetString(FieldNames.ProjectNumber),
                Overall = item.GetEnum(FieldNames.OverallStatus, RagStatus.NotAssessed),
                Schedule = item.GetEnum(FieldNames.ScheduleStatus, RagStatus.NotAssessed),
                Budget = item.GetEnum(FieldNames.BudgetStatus, RagStatus.NotAssessed),
                Risk = item.GetEnum(FieldNames.RiskStatus, RagStatus.NotAssessed),
                AssessedUtc = item.GetDate(FieldNames.AssessedDate) ?? DateTime.MinValue
            };

            var summary = item.GetString(FieldNames.StatusSummary);
            if (!string.IsNullOrEmpty(summary) && summary != "On track")
            {
                s.Reasons.AddRange(summary.Split(new[] { "; " }, StringSplitOptions.RemoveEmptyEntries));
            }

            foreach (var pair in (item.GetString(FieldNames.StatusMetrics) ?? string.Empty).Split(';'))
            {
                var kv = pair.Split('=');
                if (kv.Length != 2)
                {
                    continue;
                }

                decimal v;
                decimal.TryParse(kv[1], NumberStyles.Any, CultureInfo.InvariantCulture, out v);
                switch (kv[0])
                {
                    case "deliverables": s.DeliverablesTotal = (int)v; break;
                    case "issued": s.DeliverablesIssued = (int)v; break;
                    case "overdue": s.DeliverablesOverdue = (int)v; break;
                    case "rfis": s.OpenRfis = (int)v; break;
                    case "overdueRfis": s.OverdueRfis = (int)v; break;
                    case "highRisks": s.OpenHighRisks = (int)v; break;
                    case "overdueActions": s.OverdueActions = (int)v; break;
                    case "hours": s.HoursToDate = v; break;
                    case "budget": s.BudgetHours = v; break;
                    case "burn": s.BurnRatio = v; break;
                }
            }

            return s;
        }
    }
}
