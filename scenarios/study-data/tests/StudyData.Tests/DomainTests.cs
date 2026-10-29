using StudyData.Core.Coding;
using StudyData.Core.Design;
using StudyData.Core.EditChecks;
using StudyData.Core.Labs;
using StudyData.Core.Queries;
using StudyData.Core.Scheduling;

namespace StudyData.Tests;

public class DomainTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void QueryLifecycle()
    {
        var q = QueryWorkflow.Open(1, 2, "VSWEIGHT", QueryOrigin.Monitor, "Check weight", "monitor1", Now);
        Assert.Throws<QueryTransitionException>(() => QueryWorkflow.Reopen(q, "no", "monitor1", Now));
        QueryWorkflow.Answer(q, "Corrected", "site", Now);
        Assert.Equal(QueryState.Answered, q.State);
        QueryWorkflow.Reopen(q, "Still wrong", "monitor1", Now);
        Assert.Equal(QueryState.Open, q.State);
        Assert.Throws<QueryTransitionException>(() => QueryWorkflow.Close(q, null, "monitor1", Now));
        QueryWorkflow.Close(q, "Accepted after call", "monitor1", Now);
        Assert.Equal(QueryState.Closed, q.State);
        Assert.Equal(4, q.Messages.Count);
        Assert.Throws<QueryTransitionException>(() => QueryWorkflow.Answer(q, "late", "site", Now));
    }

    [Fact]
    public void AutomaticQueriesCloseThemselves()
    {
        var q = QueryWorkflow.Open(1, 2, "EXCARBO", QueryOrigin.EditCheck, "Deviation", "system", Now, "EX01");
        QueryWorkflow.AutoClose(q, Now);
        Assert.Equal(QueryState.Closed, q.State);

        var manual = QueryWorkflow.Open(1, 2, "EXCARBO", QueryOrigin.Monitor, "Deviation", "monitor1", Now);
        QueryWorkflow.AutoClose(manual, Now);
        Assert.Equal(QueryState.Open, manual.State);
    }

    [Fact]
    public void Reminders()
    {
        var q = QueryWorkflow.Open(1, 2, "X", QueryOrigin.Monitor, "?", "m", Now.AddDays(-8));
        Assert.True(QueryWorkflow.NeedsReminder(q, Now, 7));
        q.LastReminderAt = Now.AddDays(-2);
        Assert.False(QueryWorkflow.NeedsReminder(q, Now, 7));
    }

    [Fact]
    public void VisitScheduleHasNoDayZero()
    {
        var study = StudyData.Data.Seed.SampleStudy.Create();
        var plan = VisitSchedule.Plan(study, new DateOnly(2026, 6, 16));
        Assert.Equal(new DateOnly(2026, 6, 2), plan.Single(p => p.Definition.Code == "SCR").Target);
        Assert.Equal(new DateOnly(2026, 6, 23), plan.Single(p => p.Definition.Code == "C1D8").Target);
        var c2 = plan.Single(p => p.Definition.Code == "C2D1");
        Assert.Equal(new DateOnly(2026, 7, 7), c2.Target);
        Assert.True(VisitSchedule.InWindow(c2, new DateOnly(2026, 7, 10)));
        Assert.False(VisitSchedule.InWindow(c2, new DateOnly(2026, 7, 11)));
        Assert.Equal(1, VisitSchedule.StudyDay(new DateOnly(2026, 6, 16), new DateOnly(2026, 6, 16)));
        Assert.Equal(-1, VisitSchedule.StudyDay(new DateOnly(2026, 6, 16), new DateOnly(2026, 6, 15)));
        Assert.DoesNotContain(plan, p => p.Definition.Unscheduled);
    }

    [Fact]
    public void SubjectNumbers()
    {
        Assert.Equal("101-004", Core.Scheduling.SubjectNumbers.Format("{site}-{seq:000}", "101", 4));
        Assert.Equal("S201/12", Core.Scheduling.SubjectNumbers.Format("S{site}/{seq}", "201", 12));
    }

    [Fact]
    public void BlocksAreBalancedAndReproducible()
    {
        var arms = new[] { new Arm { Code = "A", Ratio = 1 }, new Arm { Code = "B", Ratio = 1 } };
        var seed = BlockRandomiser.Seed("FAB-ONC-201", "101", "BRCAWT");
        var list = BlockRandomiser.Generate(arms, 10, seed);
        Assert.Equal(20, list.Count);
        for (var i = 0; i < list.Count; i += 2)
        {
            Assert.Equal(1, list.Skip(i).Take(2).Count(a => a == "A"));
        }

        Assert.Equal(list, BlockRandomiser.Generate(arms, 10, BlockRandomiser.Seed("FAB-ONC-201", "101", "BRCAWT")));
    }

    [Theory]
    [InlineData("ANC", "1.2", 1.5, 7.5, LabFlag.Low, 2)]
    [InlineData("ANC", "0.4", 1.5, 7.5, LabFlag.CriticalLow, 4)]
    [InlineData("PLAT", "262", 150, 400, LabFlag.Normal, 0)]
    [InlineData("ALT", "250", 5, 45, LabFlag.CriticalHigh, 3)]
    [InlineData("ALT", "160", 5, 45, LabFlag.High, 2)]
    [InlineData("CREAT", "abc", 45, 90, LabFlag.Unknown, null)]
    public void LabGrades(string test, string value, double low, double high, LabFlag flag, int? grade)
    {
        var r = LabFlags.Evaluate(test, value, (decimal)low, (decimal)high);
        Assert.Equal(flag, r.Flag);
        Assert.Equal(grade, r.Grade);
    }

    [Fact]
    public void FieldChecks()
    {
        var f = new FieldDefinition { Code = "VSWEIGHT", Label = "Weight", Type = FieldType.Decimal, Required = true, Min = 30, Max = 200, Decimals = 1, Unit = "kg" };
        Assert.Contains(EditCheckRunner.FieldChecks(f, null), r => r.Code.EndsWith(".REQ"));
        Assert.Contains(EditCheckRunner.FieldChecks(f, "abc"), r => r.Severity == EditCheckSeverity.Hard);
        Assert.Contains(EditCheckRunner.FieldChecks(f, "68.25"), r => r.Code.EndsWith(".DEC"));
        Assert.Contains(EditCheckRunner.FieldChecks(f, "250"), r => r.Code.EndsWith(".RANGE") && r.Severity == EditCheckSeverity.Query);
        Assert.Empty(EditCheckRunner.FieldChecks(f, "68.2"));
    }

    [Fact]
    public void CodingDecision()
    {
        Assert.Equal(CodingState.NeedsReview, CodingRules.Decide(Array.Empty<CodingCandidate>()));
        var exact = new CodingCandidate("1", "Nausea", "1", "Nausea", "GI", 100);
        Assert.Equal(CodingState.AutoCoded, CodingRules.Decide(new[] { exact }));
        Assert.Equal(CodingState.NeedsReview, CodingRules.Decide(new[] { exact, exact with { Code = "2" } }));
        Assert.Equal("TINGLING IN FINGERS", CodingRules.Normalise("  tingling-in   fingers! "));
    }
}
