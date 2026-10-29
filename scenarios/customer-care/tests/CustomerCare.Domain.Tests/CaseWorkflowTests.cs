using System;
using Tailspin.CustomerCare.Domain;
using Xunit;

namespace Tailspin.CustomerCare.Domain.Tests
{
    public class CaseWorkflowTests
    {
        private static readonly DateTime Now = new DateTime(2026, 10, 29, 10, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void Taking_a_new_case_assigns_it()
        {
            var c = new Case { Status = CaseStatus.New };
            CaseWorkflow.Move(c, CaseStatus.InProgress, "CORP\\mvirtanen", Now);
            Assert.Equal("CORP\\mvirtanen", c.AssignedTo);
            Assert.Equal(CaseStatus.InProgress, c.Status);
        }

        [Fact]
        public void Returning_to_new_clears_assignee()
        {
            var c = new Case { Status = CaseStatus.Assigned, AssignedTo = "x" };
            CaseWorkflow.Move(c, CaseStatus.New, "y", Now);
            Assert.Null(c.AssignedTo);
        }

        [Fact]
        public void Reopen_clears_resolved_time()
        {
            var c = new Case { Status = CaseStatus.InProgress, AssignedTo = "x" };
            CaseWorkflow.Move(c, CaseStatus.Resolved, "x", Now);
            Assert.Equal(Now, c.ResolvedUtc);
            CaseWorkflow.Move(c, CaseStatus.InProgress, "x", Now.AddHours(1));
            Assert.Null(c.ResolvedUtc);
        }

        [Fact]
        public void Closing_sets_both_timestamps()
        {
            var c = new Case { Status = CaseStatus.WaitingOnCustomer };
            CaseWorkflow.Move(c, CaseStatus.Closed, "x", Now);
            Assert.Equal(Now, c.ClosedUtc);
            Assert.Equal(Now, c.ResolvedUtc);
            Assert.False(c.IsOpen);
        }

        [Fact]
        public void Closed_is_final()
        {
            var c = new Case { Status = CaseStatus.Closed, CaseNumber = "CC-2026-000001" };
            Assert.Throws<InvalidOperationException>(() => CaseWorkflow.Move(c, CaseStatus.InProgress, "x", Now));
            Assert.Empty(CaseWorkflow.NextStatuses(CaseStatus.Closed));
        }

        [Fact]
        public void First_response_is_kept()
        {
            var c = new Case();
            CaseWorkflow.RecordResponse(c, Now);
            CaseWorkflow.RecordResponse(c, Now.AddHours(1));
            Assert.Equal(Now, c.FirstResponseUtc);
        }

        [Fact]
        public void Display_names()
        {
            Assert.Equal("Waiting on customer", CaseWorkflow.DisplayName(CaseStatus.WaitingOnCustomer));
            Assert.Equal("New", CaseWorkflow.DisplayName(CaseStatus.New));
        }
    }
}
