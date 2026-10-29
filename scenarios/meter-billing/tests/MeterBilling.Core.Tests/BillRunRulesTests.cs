using Coho.Billing.Core.Runs;
using Xunit;

namespace Coho.Billing.Core.Tests;

public class BillRunRulesTests
{
    private static BillRunSummary Run(BillRunStage stage, int openExceptions, decimal total) =>
        new BillRunSummary { RunId = 1, Stage = stage, OpenExceptions = openExceptions, TotalAmount = total };

    [Fact]
    public void Review_waits_for_open_exceptions()
    {
        Assert.Equal(BillRunStage.Review, BillRunRules.AfterReview(Run(BillRunStage.Review, 2, 100m), 1000m, true));
    }

    [Fact]
    public void Review_is_approved_automatically_under_the_limit()
    {
        Assert.Equal(BillRunStage.Approved, BillRunRules.AfterReview(Run(BillRunStage.Review, 0, 999m), 1000m, false));
    }

    [Fact]
    public void Large_run_needs_an_operator()
    {
        Assert.Equal(BillRunStage.Review, BillRunRules.AfterReview(Run(BillRunStage.Review, 0, 5000m), 1000m, false));
        Assert.Equal(BillRunStage.Approved, BillRunRules.AfterReview(Run(BillRunStage.Review, 0, 5000m), 1000m, true));
    }

    [Theory]
    [InlineData(BillRunStage.Review, true, false, true, true)]
    [InlineData(BillRunStage.Held, false, true, true, true)]
    [InlineData(BillRunStage.Failed, false, true, true, true)]
    [InlineData(BillRunStage.Releasing, false, false, false, false)]
    [InlineData(BillRunStage.Completed, false, false, false, false)]
    public void Operator_actions_by_stage(BillRunStage stage, bool hold, bool resume, bool rerate, bool cancel)
    {
        Assert.Equal(hold, BillRunRules.CanHold(stage));
        Assert.Equal(resume, BillRunRules.CanResume(stage));
        Assert.Equal(rerate, BillRunRules.CanRerate(stage));
        Assert.Equal(cancel, BillRunRules.CanCancel(stage));
    }
}
