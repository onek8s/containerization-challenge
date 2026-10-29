using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Lucerne.ProjectSites.Domain
{
    public class Project
    {
        private static readonly Regex NumberPattern = new Regex(@"^P\d{5}$", RegexOptions.Compiled);

        public Project()
        {
            Disciplines = new List<string>();
        }

        /// <summary>ERP project number, e.g. P19041 (two-digit year + sequence).</summary>
        public string Number { get; set; }
        public string Name { get; set; }
        public string ClientName { get; set; }
        public string ClientNumber { get; set; }
        public string OfficeCode { get; set; }
        public string Sector { get; set; }
        public ProjectStage Stage { get; set; }
        public string ProjectManagerLogin { get; set; }
        public string ProjectDirectorLogin { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? PlannedEndDate { get; set; }
        public DateTime? ClosedDate { get; set; }
        public decimal BudgetHours { get; set; }
        public decimal FeeValue { get; set; }
        public string Currency { get; set; }
        public decimal PercentComplete { get; set; }
        public bool Confidential { get; set; }
        public List<string> Disciplines { get; private set; }
        public string SiteUrl { get; set; }
        public DateTime ErpModifiedUtc { get; set; }

        public static bool IsValidNumber(string number)
        {
            return number != null && NumberPattern.IsMatch(number);
        }

        /// <summary>Server-relative URL of the project site collection under the managed path.</summary>
        public static string SiteRelativeUrl(string number)
        {
            if (!IsValidNumber(number))
            {
                throw new ArgumentException("Invalid project number: " + number, nameof(number));
            }

            return "/projects/" + number.ToLowerInvariant();
        }

        public bool NeedsSite
        {
            get { return string.IsNullOrEmpty(SiteUrl) && (Stage == ProjectStage.Active || Stage == ProjectStage.Bid); }
        }
    }

    public class StaffMember
    {
        public string EmployeeId { get; set; }
        public string AccountName { get; set; }
        public string DisplayName { get; set; }
        public string Email { get; set; }
        public string OfficeCode { get; set; }
        public string DisciplineCode { get; set; }
        public string Grade { get; set; }
        public string JobTitle { get; set; }
        public string ManagerEmployeeId { get; set; }
        public bool Active { get; set; }
        public DateTime? LeavingDate { get; set; }

        /// <summary>Windows claims encoded login as SharePoint stores it.</summary>
        public string ClaimsLogin
        {
            get { return Claims.WindowsLogin(AccountName); }
        }
    }

    public static class Claims
    {
        public const string WindowsPrefix = "i:0#.w|";

        public static string WindowsLogin(string accountName)
        {
            if (string.IsNullOrEmpty(accountName))
            {
                return accountName;
            }

            return accountName.StartsWith(WindowsPrefix, StringComparison.OrdinalIgnoreCase)
                ? accountName
                : WindowsPrefix + accountName.ToLowerInvariant();
        }

        public static string AccountName(string login)
        {
            if (string.IsNullOrEmpty(login))
            {
                return login;
            }

            var bar = login.LastIndexOf('|');
            return bar >= 0 ? login.Substring(bar + 1) : login;
        }
    }

    public class TimesheetEntry
    {
        public string EmployeeId { get; set; }
        public string ProjectNumber { get; set; }
        public string ActivityCode { get; set; }
        public DateTime WorkDate { get; set; }
        public decimal Hours { get; set; }
        public bool Approved { get; set; }
    }

    public class Deliverable
    {
        public int Id { get; set; }
        public string DocumentNumber { get; set; }
        public string Title { get; set; }
        public string DisciplineCode { get; set; }
        public string DocumentType { get; set; }
        public string Revision { get; set; }
        public DeliverableStatus Status { get; set; }
        public DateTime? PlannedDate { get; set; }
        public DateTime? ForecastDate { get; set; }
        public DateTime? IssuedDate { get; set; }

        public bool IsOverdue(DateTime today)
        {
            if (Status == DeliverableStatus.Issued || Status == DeliverableStatus.Superseded || Status == DeliverableStatus.Withdrawn)
            {
                return false;
            }

            var due = ForecastDate ?? PlannedDate;
            return due.HasValue && due.Value.Date < today.Date;
        }

        public bool IsSlipping
        {
            get { return PlannedDate.HasValue && ForecastDate.HasValue && ForecastDate.Value.Date > PlannedDate.Value.Date; }
        }
    }

    public class Risk
    {
        public string Title { get; set; }
        public RiskSeverity Severity { get; set; }
        public bool Open { get; set; }
        public string OwnerLogin { get; set; }
    }

    public class Rfi
    {
        public string Reference { get; set; }
        public string Subject { get; set; }
        public DateTime Raised { get; set; }
        public DateTime? ResponseDue { get; set; }
        public bool Closed { get; set; }
    }

    public class ActionItem
    {
        public string Title { get; set; }
        public DateTime? DueDate { get; set; }
        public bool Completed { get; set; }
        public string AssignedToLogin { get; set; }
    }
}
