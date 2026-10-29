using System;
using System.Collections.Generic;

namespace Tailspin.CustomerCare.Domain
{
    public static class CaseWorkflow
    {
        private static readonly Dictionary<CaseStatus, CaseStatus[]> Allowed = new Dictionary<CaseStatus, CaseStatus[]>
        {
            { CaseStatus.New, new[] { CaseStatus.Assigned, CaseStatus.InProgress, CaseStatus.Resolved, CaseStatus.Closed } },
            { CaseStatus.Assigned, new[] { CaseStatus.New, CaseStatus.InProgress, CaseStatus.WaitingOnCustomer, CaseStatus.Resolved } },
            { CaseStatus.InProgress, new[] { CaseStatus.Assigned, CaseStatus.WaitingOnCustomer, CaseStatus.Resolved } },
            { CaseStatus.WaitingOnCustomer, new[] { CaseStatus.InProgress, CaseStatus.Resolved, CaseStatus.Closed } },
            { CaseStatus.Resolved, new[] { CaseStatus.InProgress, CaseStatus.Closed } },
            { CaseStatus.Closed, new CaseStatus[0] }
        };

        public static bool CanMove(CaseStatus from, CaseStatus to)
        {
            if (from == to) return true;
            return Array.IndexOf(Allowed[from], to) >= 0;
        }

        public static IEnumerable<CaseStatus> NextStatuses(CaseStatus from)
        {
            return Allowed[from];
        }

        /// <summary>Applies a status change and keeps the timestamps consistent.</summary>
        public static void Move(Case c, CaseStatus to, string user, DateTime nowUtc)
        {
            if (c == null) throw new ArgumentNullException(nameof(c));
            if (!CanMove(c.Status, to))
            {
                throw new InvalidOperationException(string.Format("Case {0} cannot move from {1} to {2}.", c.CaseNumber, c.Status, to));
            }

            if ((to == CaseStatus.Assigned || to == CaseStatus.InProgress) && string.IsNullOrEmpty(c.AssignedTo))
            {
                c.AssignedTo = user;
            }

            if (to == CaseStatus.New)
            {
                c.AssignedTo = null;
            }

            if (to == CaseStatus.Resolved)
            {
                c.ResolvedUtc = nowUtc;
            }
            else if (to == CaseStatus.InProgress && c.Status == CaseStatus.Resolved)
            {
                c.ResolvedUtc = null;
            }

            if (to == CaseStatus.Closed)
            {
                c.ClosedUtc = nowUtc;
                if (c.ResolvedUtc == null) c.ResolvedUtc = nowUtc;
            }

            c.Status = to;
            c.ModifiedUtc = nowUtc;
        }

        /// <summary>The first note visible to the customer, or any status change by an agent, counts as the first response.</summary>
        public static void RecordResponse(Case c, DateTime nowUtc)
        {
            if (c.FirstResponseUtc == null) c.FirstResponseUtc = nowUtc;
        }

        public static string DisplayName(CaseStatus status)
        {
            switch (status)
            {
                case CaseStatus.InProgress: return "In progress";
                case CaseStatus.WaitingOnCustomer: return "Waiting on customer";
                default: return status.ToString();
            }
        }
    }
}
