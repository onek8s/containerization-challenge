using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Lucerne.ProjectSites.Domain
{
    public class ProjectSnapshot
    {
        public ProjectSnapshot()
        {
            Deliverables = new List<Deliverable>();
            Risks = new List<Risk>();
            Rfis = new List<Rfi>();
            Actions = new List<ActionItem>();
        }

        public Project Project { get; set; }
        public List<Deliverable> Deliverables { get; private set; }
        public List<Risk> Risks { get; private set; }
        public List<Rfi> Rfis { get; private set; }
        public List<ActionItem> Actions { get; private set; }
        public decimal HoursToDate { get; set; }

        /// <summary>RAG set manually by the PM on the project site; overrides the calculated value when newer than a week.</summary>
        public RagStatus? ManualStatus { get; set; }
        public DateTime? ManualStatusDate { get; set; }
        public string ManualStatusComment { get; set; }
    }

    public class ProjectStatus
    {
        public ProjectStatus()
        {
            Reasons = new List<string>();
        }

        public string ProjectNumber { get; set; }
        public RagStatus Overall { get; set; }
        public RagStatus Schedule { get; set; }
        public RagStatus Budget { get; set; }
        public RagStatus Risk { get; set; }
        public int DeliverablesTotal { get; set; }
        public int DeliverablesIssued { get; set; }
        public int DeliverablesOverdue { get; set; }
        public int OpenRfis { get; set; }
        public int OverdueRfis { get; set; }
        public int OpenHighRisks { get; set; }
        public int OverdueActions { get; set; }
        public decimal HoursToDate { get; set; }
        public decimal BudgetHours { get; set; }
        public decimal BurnRatio { get; set; }
        public bool ManualOverride { get; set; }
        public List<string> Reasons { get; private set; }
        public DateTime AssessedUtc { get; set; }

        public string Summary
        {
            get { return Reasons.Count == 0 ? "On track" : string.Join("; ", Reasons); }
        }
    }

    /// <summary>
    /// RAG thresholds agreed with the operations board in 2016 (LE-QP-12). Schedule: share of deliverables
    /// overdue. Budget: hours burnt against the PM's percent complete. Risk: open high/critical risks and
    /// overdue RFIs.
    /// </summary>
    public class StatusAssessor
    {
        public StatusAssessor()
        {
            ScheduleAmberPercent = 10m;
            ScheduleRedPercent = 25m;
            BurnAmber = 1.10m;
            BurnRed = 1.25m;
            RfiResponseDays = 10;
            ManualOverrideDays = 7;
        }

        public decimal ScheduleAmberPercent { get; set; }
        public decimal ScheduleRedPercent { get; set; }
        public decimal BurnAmber { get; set; }
        public decimal BurnRed { get; set; }
        public int RfiResponseDays { get; set; }
        public int ManualOverrideDays { get; set; }

        public ProjectStatus Assess(ProjectSnapshot snapshot, DateTime today)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            var p = snapshot.Project;
            var status = new ProjectStatus
            {
                ProjectNumber = p.Number,
                AssessedUtc = DateTime.UtcNow,
                HoursToDate = snapshot.HoursToDate,
                BudgetHours = p.BudgetHours
            };

            var live = snapshot.Deliverables.Where(d => d.Status != DeliverableStatus.Withdrawn).ToList();
            status.DeliverablesTotal = live.Count;
            status.DeliverablesIssued = live.Count(d => d.Status == DeliverableStatus.Issued || d.Status == DeliverableStatus.Superseded);
            status.DeliverablesOverdue = live.Count(d => d.IsOverdue(today));

            if (live.Count == 0)
            {
                status.Schedule = RagStatus.NotAssessed;
            }
            else
            {
                var overduePct = 100m * status.DeliverablesOverdue / live.Count;
                status.Schedule = overduePct >= ScheduleRedPercent ? RagStatus.Red
                    : overduePct >= ScheduleAmberPercent ? RagStatus.Amber
                    : RagStatus.Green;
                if (status.Schedule != RagStatus.Green)
                {
                    status.Reasons.Add(string.Format(CultureInfo.InvariantCulture, "{0} of {1} deliverables overdue", status.DeliverablesOverdue, live.Count));
                }
            }

            if (p.BudgetHours <= 0)
            {
                status.Budget = RagStatus.NotAssessed;
            }
            else
            {
                var spent = snapshot.HoursToDate / p.BudgetHours;
                var complete = Math.Max(p.PercentComplete, 5m) / 100m;
                status.BurnRatio = Math.Round(spent / complete, 2);
                status.Budget = status.BurnRatio >= BurnRed || spent > 1m ? RagStatus.Red
                    : status.BurnRatio >= BurnAmber ? RagStatus.Amber
                    : RagStatus.Green;
                if (status.Budget != RagStatus.Green)
                {
                    status.Reasons.Add(string.Format(CultureInfo.InvariantCulture, "{0:0}% of budget hours used at {1:0}% complete", spent * 100m, p.PercentComplete));
                }
            }

            status.OpenHighRisks = snapshot.Risks.Count(r => r.Open && r.Severity >= RiskSeverity.High);
            var critical = snapshot.Risks.Any(r => r.Open && r.Severity == RiskSeverity.Critical);
            status.OpenRfis = snapshot.Rfis.Count(r => !r.Closed);
            status.OverdueRfis = snapshot.Rfis.Count(r => !r.Closed && (r.ResponseDue ?? r.Raised.AddDays(RfiResponseDays)).Date < today.Date);
            status.OverdueActions = snapshot.Actions.Count(a => !a.Completed && a.DueDate.HasValue && a.DueDate.Value.Date < today.Date);

            status.Risk = critical || status.OpenHighRisks >= 3 ? RagStatus.Red
                : status.OpenHighRisks > 0 || status.OverdueRfis >= 3 ? RagStatus.Amber
                : RagStatus.Green;
            if (status.OpenHighRisks > 0)
            {
                status.Reasons.Add(status.OpenHighRisks + " open high risk(s)" + (critical ? " incl. critical" : string.Empty));
            }

            if (status.OverdueRfis > 0)
            {
                status.Reasons.Add(status.OverdueRfis + " RFI(s) past response date");
            }

            status.Overall = Worst(status.Schedule, status.Budget, status.Risk);

            if (snapshot.ManualStatus.HasValue && snapshot.ManualStatusDate.HasValue
                && (today.Date - snapshot.ManualStatusDate.Value.Date).TotalDays <= ManualOverrideDays)
            {
                status.Overall = snapshot.ManualStatus.Value;
                status.ManualOverride = true;
                status.Reasons.Insert(0, "PM override: " + (snapshot.ManualStatusComment ?? "no comment"));
            }

            return status;
        }

        public static RagStatus Worst(params RagStatus[] values)
        {
            var assessed = values.Where(v => v != RagStatus.NotAssessed).ToList();
            return assessed.Count == 0 ? RagStatus.NotAssessed : assessed.Max();
        }
    }
}
