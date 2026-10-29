using System;
using Xunit;

namespace Contoso.Purchasing.Core.Tests
{
    public class ReminderPolicyTests
    {
        private readonly ReminderPolicy _policy = new ReminderPolicy(2, 1);

        [Fact]
        public void Weekend_is_not_counted()
        {
            // Friday to Monday is one business day.
            Assert.Equal(1, BusinessCalendar.BusinessDaysBetween(new DateTime(2026, 3, 6, 15, 0, 0), new DateTime(2026, 3, 9, 6, 0, 0)));
        }

        [Fact]
        public void First_reminder_after_two_business_days()
        {
            var request = TestData.Request(100m);
            request.Status = RequestStatus.InApproval;
            request.SubmittedAt = new DateTime(2026, 3, 5, 14, 0, 0); // Thursday

            Assert.False(_policy.IsDue(request, new DateTime(2026, 3, 6, 6, 0, 0)));
            Assert.False(_policy.IsDue(request, new DateTime(2026, 3, 8, 6, 0, 0)));
            Assert.True(_policy.IsDue(request, new DateTime(2026, 3, 9, 6, 0, 0)));
        }

        [Fact]
        public void Reminder_is_repeated_daily()
        {
            var request = TestData.Request(100m);
            request.Status = RequestStatus.InApproval;
            request.SubmittedAt = new DateTime(2026, 3, 2, 14, 0, 0);
            request.LastReminderAt = new DateTime(2026, 3, 4, 6, 0, 0);

            Assert.False(_policy.IsDue(request, new DateTime(2026, 3, 4, 18, 0, 0)));
            Assert.True(_policy.IsDue(request, new DateTime(2026, 3, 5, 6, 0, 0)));
        }

        [Fact]
        public void Approval_resets_waiting_time()
        {
            var request = TestData.Request(3000m);
            request.Status = RequestStatus.InApproval;
            request.SubmittedAt = new DateTime(2026, 3, 2, 14, 0, 0);
            request.LastReminderAt = new DateTime(2026, 3, 4, 6, 0, 0);
            request.Approvals.Add(new ApprovalRecord { Step = ApprovalStep.Manager, Decision = ApprovalDecision.Approved, DecidedAt = new DateTime(2026, 3, 5, 9, 0, 0) });

            Assert.False(_policy.IsDue(request, new DateTime(2026, 3, 6, 6, 0, 0)));
            Assert.True(_policy.IsDue(request, new DateTime(2026, 3, 9, 6, 0, 0)));
        }

        [Fact]
        public void Drafts_are_never_reminded()
        {
            var request = TestData.Request(100m);
            Assert.False(_policy.IsDue(request, new DateTime(2026, 6, 1)));
        }
    }
}
