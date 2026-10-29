using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Lucerne.ProjectSites.Domain;

namespace Lucerne.ProjectSites.Documents
{
    /// <summary>Monthly project status report (Word), exported from the project site and attached to the PM pack.</summary>
    public static class StatusReportDocument
    {
        public static byte[] Create(ProjectSnapshot snapshot, ProjectStatus status, string preparedBy, DateTime reportDate)
        {
            var p = snapshot.Project;
            using (var w = new WordBuilder(p.Number + " status report " + reportDate.ToString("yyyy-MM"), preparedBy))
            {
                w.Heading(p.Number + " " + p.Name);
                w.Paragraph("Project status report " + reportDate.ToString("d MMMM yyyy", CultureInfo.InvariantCulture), true);

                w.Table(
                    new[] { "Client", "Office", "Stage", "Project manager", "% complete" },
                    new[] { new[] { p.ClientName, p.OfficeCode, p.Stage.ToString(), Claims.AccountName(p.ProjectManagerLogin), p.PercentComplete.ToString("0", CultureInfo.InvariantCulture) + "%" } });

                w.Heading("Status", 2);
                var rag = new[] { status.Overall, status.Schedule, status.Budget, status.Risk };
                w.Table(
                    new[] { "Overall", "Schedule", "Budget", "Risk" },
                    new[] { rag.Select(r => r.ToString()).ToArray() },
                    (row, col) => RagColours.Hex(rag[col]));

                w.Bullets(status.Reasons.Count == 0 ? new[] { "On track" } : (IEnumerable<string>)status.Reasons);

                w.Heading("Key figures", 2);
                w.Table(
                    new[] { "Measure", "Value" },
                    new[]
                    {
                        new[] { "Deliverables issued", status.DeliverablesIssued + " / " + status.DeliverablesTotal },
                        new[] { "Deliverables overdue", status.DeliverablesOverdue.ToString(CultureInfo.InvariantCulture) },
                        new[] { "Hours to date", status.HoursToDate.ToString("N0", CultureInfo.InvariantCulture) + " of " + status.BudgetHours.ToString("N0", CultureInfo.InvariantCulture) },
                        new[] { "Burn ratio", status.BurnRatio.ToString("0.00", CultureInfo.InvariantCulture) },
                        new[] { "Open RFIs (overdue)", status.OpenRfis + " (" + status.OverdueRfis + ")" },
                        new[] { "Open high risks", status.OpenHighRisks.ToString(CultureInfo.InvariantCulture) },
                        new[] { "Overdue actions", status.OverdueActions.ToString(CultureInfo.InvariantCulture) }
                    });

                var upcoming = snapshot.Deliverables
                    .Where(d => d.Status != DeliverableStatus.Issued && d.Status != DeliverableStatus.Superseded && d.Status != DeliverableStatus.Withdrawn)
                    .OrderBy(d => d.ForecastDate ?? d.PlannedDate ?? DateTime.MaxValue)
                    .Take(15)
                    .ToList();
                w.Heading("Upcoming deliverables", 2);
                w.Table(
                    new[] { "Number", "Title", "Rev", "Status", "Planned", "Forecast" },
                    upcoming.Select(d => new[] { d.DocumentNumber, d.Title, d.Revision, d.Status.ToString(), Date(d.PlannedDate), Date(d.ForecastDate) }),
                    (row, col) => upcoming[row].IsOverdue(reportDate) ? "FFC7CE" : null);

                var risks = snapshot.Risks.Where(r => r.Open).OrderByDescending(r => r.Severity).Take(10).ToList();
                w.Heading("Top risks", 2);
                w.Table(new[] { "Risk", "Severity", "Owner" }, risks.Select(r => new[] { r.Title, r.Severity.ToString(), Claims.AccountName(r.OwnerLogin) }));

                w.Paragraph("Prepared by " + preparedBy + ". Generated from the project site.");
                return w.ToArray();
            }
        }

        private static string Date(DateTime? d)
        {
            return d.HasValue ? d.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : string.Empty;
        }
    }

    public class TransmittalLine
    {
        public string DocumentNumber { get; set; }
        public string Title { get; set; }
        public string Revision { get; set; }
        public string Purpose { get; set; }
    }

    /// <summary>Transmittal cover sheet for documents sent to the client or contractors.</summary>
    public static class TransmittalSheet
    {
        public static byte[] Create(string projectNumber, string projectName, string transmittalNumber, string recipient, string sender, DateTime date, IList<TransmittalLine> lines)
        {
            using (var w = new WordBuilder(transmittalNumber, sender))
            {
                w.Heading("Transmittal " + transmittalNumber);
                w.Table(
                    new[] { "Project", "To", "From", "Date" },
                    new[] { new[] { projectNumber + " " + projectName, recipient, sender, date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) } });
                w.Heading("Documents", 2);
                w.Table(new[] { "Document number", "Title", "Revision", "Purpose" }, lines.Select(l => new[] { l.DocumentNumber, l.Title, l.Revision, l.Purpose }));
                w.Paragraph(lines.Count + " document(s). Please acknowledge receipt within 5 working days.");
                return w.ToArray();
            }
        }
    }
}
