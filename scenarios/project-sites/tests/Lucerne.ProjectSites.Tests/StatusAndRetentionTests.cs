using System;
using System.Linq;
using Lucerne.ProjectSites.Domain;
using Xunit;

namespace Lucerne.ProjectSites.Tests
{
    public class StatusAssessorTests
    {
        private static readonly DateTime Today = new DateTime(2019, 3, 15);

        private static ProjectSnapshot Snapshot(int deliverables, int overdue, decimal hours, decimal budget = 1000, decimal complete = 50)
        {
            var s = new ProjectSnapshot { Project = new Project { Number = "P19001", BudgetHours = budget, PercentComplete = complete }, HoursToDate = hours };
            for (var i = 0; i < deliverables; i++)
            {
                s.Deliverables.Add(new Deliverable { Status = DeliverableStatus.WorkInProgress, PlannedDate = i < overdue ? Today.AddDays(-5) : Today.AddDays(30) });
            }

            return s;
        }

        [Fact]
        public void Healthy_project_is_green()
        {
            var status = new StatusAssessor().Assess(Snapshot(20, 0, 450), Today);
            Assert.Equal(RagStatus.Green, status.Overall);
            Assert.Equal("On track", status.Summary);
            Assert.Equal(0.9m, status.BurnRatio);
        }

        [Fact]
        public void Overdue_deliverables_drive_schedule()
        {
            Assert.Equal(RagStatus.Amber, new StatusAssessor().Assess(Snapshot(20, 2, 400), Today).Schedule);
            Assert.Equal(RagStatus.Red, new StatusAssessor().Assess(Snapshot(20, 5, 400), Today).Schedule);
        }

        [Fact]
        public void Burn_ratio_drives_budget()
        {
            Assert.Equal(RagStatus.Amber, new StatusAssessor().Assess(Snapshot(10, 0, 560), Today).Budget);
            Assert.Equal(RagStatus.Red, new StatusAssessor().Assess(Snapshot(10, 0, 640), Today).Budget);
            Assert.Equal(RagStatus.Red, new StatusAssessor().Assess(Snapshot(10, 0, 1010, complete: 100), Today).Budget);
        }

        [Fact]
        public void Critical_risk_is_red_and_issued_items_are_not_overdue()
        {
            var s = Snapshot(4, 0, 100);
            s.Deliverables.Add(new Deliverable { Status = DeliverableStatus.Issued, PlannedDate = Today.AddDays(-50) });
            s.Risks.Add(new Risk { Open = true, Severity = RiskSeverity.Critical, Title = "x" });
            var status = new StatusAssessor().Assess(s, Today);
            Assert.Equal(RagStatus.Red, status.Risk);
            Assert.Equal(0, status.DeliverablesOverdue);
            Assert.Equal(1, status.DeliverablesIssued);
        }

        [Fact]
        public void Manual_override_applies_for_seven_days()
        {
            var s = Snapshot(10, 5, 900);
            s.ManualStatus = RagStatus.Green;
            s.ManualStatusDate = Today.AddDays(-3);
            s.ManualStatusComment = "Client accepted new programme";
            var status = new StatusAssessor().Assess(s, Today);
            Assert.Equal(RagStatus.Green, status.Overall);
            Assert.True(status.ManualOverride);
            Assert.StartsWith("PM override", status.Reasons[0]);

            s.ManualStatusDate = Today.AddDays(-8);
            Assert.Equal(RagStatus.Red, new StatusAssessor().Assess(s, Today).Overall);
        }

        [Fact]
        public void No_data_is_not_assessed()
        {
            var status = new StatusAssessor().Assess(Snapshot(0, 0, 0, budget: 0), Today);
            Assert.Equal(RagStatus.Green, status.Overall); // risk only
            Assert.Equal(RagStatus.NotAssessed, status.Schedule);
            Assert.Equal(RagStatus.NotAssessed, status.Budget);
        }
    }

    public class RetentionPolicyTests
    {
        private static readonly DateTime Closed = new DateTime(2016, 6, 30);

        [Theory]
        [InlineData("DWG", DeliverableStatus.Issued, "Design")]
        [InlineData("CAL", DeliverableStatus.Issued, "Calculation")]
        [InlineData("COR", DeliverableStatus.Issued, "Correspondence")]
        [InlineData("SPC", DeliverableStatus.Approved, "Contract")]
        [InlineData("DWG", DeliverableStatus.WorkInProgress, "WorkInProgress")]
        [InlineData("RPT", DeliverableStatus.Withdrawn, "WorkInProgress")]
        public void Category(string type, DeliverableStatus status, string expected)
        {
            Assert.Equal(expected, RetentionPolicy.CategoryFor(type, status));
        }

        [Fact]
        public void Open_projects_keep_everything()
        {
            Assert.Equal(RetentionAction.Keep, new RetentionPolicy().Decide("x", "WorkInProgress", false, null, DateTime.Today).Action);
        }

        [Fact]
        public void Design_records_declared_then_archived_after_ten_years()
        {
            var policy = new RetentionPolicy();
            Assert.Equal(RetentionAction.DeclareRecord, policy.Decide("x", "Design", false, Closed, Closed.AddYears(1)).Action);
            Assert.Equal(RetentionAction.Keep, policy.Decide("x", "Design", true, Closed, Closed.AddYears(5)).Action);
            var archive = policy.Decide("x", "Design", true, Closed, Closed.AddYears(10));
            Assert.Equal(RetentionAction.MoveToArchive, archive.Action);
            Assert.Equal(Closed.AddYears(10), archive.DueDate);
        }

        [Fact]
        public void Working_files_deleted_after_a_year_but_records_need_review()
        {
            var policy = new RetentionPolicy();
            Assert.Equal(RetentionAction.Keep, policy.Decide("x", "WorkInProgress", false, Closed, Closed.AddMonths(11)).Action);
            Assert.Equal(RetentionAction.Delete, policy.Decide("x", "WorkInProgress", false, Closed, Closed.AddYears(1)).Action);
            Assert.Equal(RetentionAction.Keep, policy.Decide("x", "Correspondence", true, Closed, Closed.AddYears(8)).Action);
            Assert.Equal(RetentionAction.Keep, policy.Decide("x", "Unknown", false, Closed, Closed.AddYears(30)).Action);
        }

        [Fact]
        public void Default_rules_cover_all_categories()
        {
            var categories = new RetentionPolicy().Rules.Select(r => r.Category).ToList();
            foreach (var c in new[] { "Contract", "Design", "Calculation", "Correspondence", "Minutes", "WorkInProgress" })
            {
                Assert.Contains(c, categories);
            }
        }
    }
}
