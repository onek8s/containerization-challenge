using System;
using Tailspin.CustomerCare.Domain;
using Xunit;

namespace Tailspin.CustomerCare.Domain.Tests
{
    public class SlaPolicyTests
    {
        private static readonly DateTime Created = new DateTime(2026, 10, 26, 8, 0, 0, DateTimeKind.Utc); // Monday

        private static SlaPolicy Policy()
        {
            return new SlaPolicy(BusinessCalendarTests.Calendar(), new[]
            {
                new SlaTarget { Priority = CasePriority.Low, ResponseHours = 18, ResolveHours = 90 },
                new SlaTarget { Priority = CasePriority.Normal, ResponseHours = 9, ResolveHours = 36 },
                new SlaTarget { Priority = CasePriority.High, ResponseHours = 4, ResolveHours = 18 },
                new SlaTarget { Priority = CasePriority.Critical, ResponseHours = 1, ResolveHours = 8 }
            });
        }

        private static Case NewCase(CasePriority p)
        {
            var due = Policy().Calculate(Created, p, false);
            return new Case { CaseNumber = "CC-2026-000001", CreatedUtc = Created, Priority = p, Status = CaseStatus.Assigned, ResponseDueUtc = due.ResponseDueUtc, ResolveDueUtc = due.ResolveDueUtc };
        }

        [Fact]
        public void Normal_case_due_end_of_day_and_after_four_working_days()
        {
            var due = Policy().Calculate(Created, CasePriority.Normal, false);
            Assert.Equal(new DateTime(2026, 10, 26, 17, 0, 0), due.ResponseDueUtc);
            Assert.Equal(new DateTime(2026, 10, 29, 17, 0, 0), due.ResolveDueUtc);
        }

        [Fact]
        public void Priority_customer_halves_targets()
        {
            var due = Policy().Calculate(Created, CasePriority.Critical, true);
            Assert.Equal(new DateTime(2026, 10, 26, 8, 30, 0), due.ResponseDueUtc);
            Assert.Equal(new DateTime(2026, 10, 26, 12, 0, 0), due.ResolveDueUtc);
        }

        [Fact]
        public void Evaluate_on_track_at_risk_and_breached()
        {
            var c = NewCase(CasePriority.Critical);
            c.FirstResponseUtc = Created.AddMinutes(10);
            var p = Policy();
            Assert.Equal(SlaState.OnTrack, p.Evaluate(c, Created.AddHours(2)));
            Assert.Equal(SlaState.AtRisk, p.Evaluate(c, Created.AddHours(7)));
            Assert.Equal(SlaState.Breached, p.Evaluate(c, Created.AddHours(9)));
        }

        [Fact]
        public void Missed_first_response_is_breach()
        {
            var c = NewCase(CasePriority.Critical);
            Assert.Equal(SlaState.Breached, Policy().Evaluate(c, Created.AddHours(2)));
            Assert.Equal(2, Policy().EscalationLevel(c, Created.AddHours(2)));
        }

        [Fact]
        public void Resolved_case_reports_met_or_breached()
        {
            var c = NewCase(CasePriority.High);
            c.Status = CaseStatus.Resolved;
            c.ResolvedUtc = c.ResolveDueUtc.AddMinutes(-1);
            Assert.Equal(SlaState.Met, Policy().Evaluate(c, DateTime.UtcNow));
            c.ResolvedUtc = c.ResolveDueUtc.AddMinutes(1);
            Assert.Equal(SlaState.Breached, Policy().Evaluate(c, DateTime.UtcNow));
        }

        [Fact]
        public void Escalation_levels()
        {
            var c = NewCase(CasePriority.Critical);
            c.FirstResponseUtc = Created;
            var p = Policy();
            Assert.Equal(0, p.EscalationLevel(c, Created.AddHours(1)));
            Assert.Equal(1, p.EscalationLevel(c, Created.AddHours(6)));
            Assert.Equal(2, p.EscalationLevel(c, Created.AddHours(8.5)));
            // 12 working hours = 150% of 8
            Assert.Equal(3, p.EscalationLevel(c, Created.AddDays(1).AddHours(3)));
            c.Status = CaseStatus.Closed;
            Assert.Equal(0, p.EscalationLevel(c, Created.AddDays(10)));
        }

        [Fact]
        public void Missing_target_throws()
        {
            var p = new SlaPolicy(BusinessCalendarTests.Calendar(), new SlaTarget[0]);
            Assert.Throws<InvalidOperationException>(() => p.TargetFor(CasePriority.Low));
        }
    }
}
