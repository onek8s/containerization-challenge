using System;
using System.Collections.Generic;
using System.Linq;

namespace Tailspin.CustomerCare.Domain
{
    public enum SlaState
    {
        OnTrack,
        AtRisk,
        Breached,
        Met
    }

    public class SlaDue
    {
        public DateTime ResponseDueUtc { get; set; }
        public DateTime ResolveDueUtc { get; set; }
    }

    /// <summary>Response and resolution targets per priority, measured in business hours.</summary>
    public class SlaPolicy
    {
        /// <summary>Share of the resolution time after which a case is reported as at risk.</summary>
        public const double AtRiskThreshold = 0.75;

        private readonly Dictionary<CasePriority, SlaTarget> _targets;
        private readonly BusinessCalendar _calendar;

        public SlaPolicy(BusinessCalendar calendar, IEnumerable<SlaTarget> targets)
        {
            _calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
            _targets = (targets ?? throw new ArgumentNullException(nameof(targets))).ToDictionary(t => t.Priority);
        }

        public BusinessCalendar Calendar
        {
            get { return _calendar; }
        }

        public SlaTarget TargetFor(CasePriority priority)
        {
            SlaTarget t;
            if (_targets.TryGetValue(priority, out t)) return t;
            throw new InvalidOperationException("No SLA target configured for priority " + priority + ".");
        }

        /// <summary>
        /// Priority customers (critical infrastructure, medical dependency) get half the normal targets.
        /// </summary>
        public SlaDue Calculate(DateTime createdUtc, CasePriority priority, bool priorityCustomer)
        {
            var t = TargetFor(priority);
            var factor = priorityCustomer ? 0.5 : 1.0;
            return new SlaDue
            {
                ResponseDueUtc = _calendar.AddBusinessHours(createdUtc, t.ResponseHours * factor),
                ResolveDueUtc = _calendar.AddBusinessHours(createdUtc, t.ResolveHours * factor)
            };
        }

        public SlaState Evaluate(Case c, DateTime nowUtc)
        {
            if (c == null) throw new ArgumentNullException(nameof(c));

            if (!c.IsOpen)
            {
                var resolved = c.ResolvedUtc ?? c.ClosedUtc ?? nowUtc;
                return resolved <= c.ResolveDueUtc ? SlaState.Met : SlaState.Breached;
            }

            if (nowUtc > c.ResolveDueUtc) return SlaState.Breached;
            if (c.FirstResponseUtc == null && nowUtc > c.ResponseDueUtc) return SlaState.Breached;

            return ConsumedShare(c, nowUtc) >= AtRiskThreshold ? SlaState.AtRisk : SlaState.OnTrack;
        }

        public double ConsumedShare(Case c, DateTime nowUtc)
        {
            var total = _calendar.BusinessTimeBetween(c.CreatedUtc, c.ResolveDueUtc);
            if (total <= TimeSpan.Zero) return 1.0;
            var used = _calendar.BusinessTimeBetween(c.CreatedUtc, nowUtc);
            return used.TotalMinutes / total.TotalMinutes;
        }

        /// <summary>
        /// 0 = nothing to do, 1 = at risk (notify assignee), 2 = breached (notify queue supervisor),
        /// 3 = breached and 50% over the target (notify contact centre manager).
        /// </summary>
        public int EscalationLevel(Case c, DateTime nowUtc)
        {
            if (!c.IsOpen) return 0;

            var share = ConsumedShare(c, nowUtc);
            var responseMissed = c.FirstResponseUtc == null && nowUtc > c.ResponseDueUtc;

            if (share >= 1.5) return 3;
            if (share >= 1.0 || responseMissed) return 2;
            if (share >= AtRiskThreshold) return 1;
            return 0;
        }
    }
}
