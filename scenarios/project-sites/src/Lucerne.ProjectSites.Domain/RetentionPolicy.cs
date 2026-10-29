using System;
using System.Collections.Generic;
using System.Linq;

namespace Lucerne.ProjectSites.Domain
{
    public class RetentionRule
    {
        public RetentionRule(string category, int yearsAfterClose, RetentionAction action, string reference)
        {
            Category = category;
            YearsAfterClose = yearsAfterClose;
            Action = action;
            Reference = reference;
        }

        public string Category { get; }
        public int YearsAfterClose { get; }
        public RetentionAction Action { get; }
        public string Reference { get; }
    }

    public class RetentionDecision
    {
        public string ItemUrl { get; set; }
        public string Category { get; set; }
        public RetentionAction Action { get; set; }
        public DateTime? DueDate { get; set; }
        public string Reason { get; set; }
    }

    /// <summary>
    /// File plan LE-RM-02 (2017). Issued and contractual documents are declared records at close-out and kept
    /// for the liability period; working files are removed after one year.
    /// </summary>
    public class RetentionPolicy
    {
        private readonly Dictionary<string, RetentionRule> _rules;

        public RetentionPolicy()
            : this(Defaults())
        {
        }

        public RetentionPolicy(IEnumerable<RetentionRule> rules)
        {
            _rules = rules.ToDictionary(r => r.Category, StringComparer.OrdinalIgnoreCase);
        }

        public static IEnumerable<RetentionRule> Defaults()
        {
            yield return new RetentionRule("Contract", 10, RetentionAction.MoveToArchive, "CO Art. 127 / SIA 118");
            yield return new RetentionRule("Design", 10, RetentionAction.MoveToArchive, "SIA 103 liability period");
            yield return new RetentionRule("Calculation", 10, RetentionAction.MoveToArchive, "SIA 260");
            yield return new RetentionRule("Correspondence", 7, RetentionAction.Delete, "LE-RM-02 4.3");
            yield return new RetentionRule("Minutes", 7, RetentionAction.Delete, "LE-RM-02 4.3");
            yield return new RetentionRule("WorkInProgress", 1, RetentionAction.Delete, "LE-RM-02 4.6");
        }

        public IReadOnlyCollection<RetentionRule> Rules
        {
            get { return _rules.Values; }
        }

        public static string CategoryFor(string documentType, DeliverableStatus status)
        {
            if (status == DeliverableStatus.Planned || status == DeliverableStatus.WorkInProgress || status == DeliverableStatus.Withdrawn)
            {
                return "WorkInProgress";
            }

            switch (documentType)
            {
                case "CAL": return "Calculation";
                case "COR":
                case "TRN": return "Correspondence";
                case "MIN": return "Minutes";
                case "SPC": return "Contract";
                default: return "Design";
            }
        }

        public RetentionDecision Decide(string itemUrl, string category, bool isRecord, DateTime? projectClosed, DateTime today)
        {
            var decision = new RetentionDecision { ItemUrl = itemUrl, Category = category, Action = RetentionAction.Keep };

            if (!projectClosed.HasValue)
            {
                decision.Reason = "Project not closed";
                return decision;
            }

            RetentionRule rule;
            if (!_rules.TryGetValue(category ?? string.Empty, out rule))
            {
                decision.Reason = "No rule for category '" + category + "', kept";
                return decision;
            }

            var due = projectClosed.Value.Date.AddYears(rule.YearsAfterClose);
            decision.DueDate = due;

            if (rule.Action != RetentionAction.Delete && !isRecord)
            {
                // Records are declared at close-out regardless of the retention period.
                decision.Action = RetentionAction.DeclareRecord;
                decision.Reason = "Declare record at close-out (" + rule.Reference + ")";
                return decision;
            }

            if (today.Date < due)
            {
                decision.Reason = "Retained until " + due.ToString("yyyy-MM-dd");
                return decision;
            }

            if (isRecord && rule.Action == RetentionAction.Delete)
            {
                decision.Reason = "Declared record, needs records manager review";
                return decision;
            }

            decision.Action = rule.Action;
            decision.Reason = rule.Action + " after " + rule.YearsAfterClose + " years (" + rule.Reference + ")";
            return decision;
        }
    }
}
