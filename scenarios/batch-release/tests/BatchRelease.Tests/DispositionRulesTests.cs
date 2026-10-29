using BatchRelease.Core.Model;
using BatchRelease.Core.Rules;

namespace BatchRelease.Tests;

public class DispositionRulesTests
{
    private readonly DispositionRules _rules = new(TestData.Clock());

    private static IEnumerable<string> Codes(Assessment a) => a.Blocking.Select(f => f.Code);

    [Fact]
    public void Complete_batch_can_be_released_by_a_second_person()
    {
        var a = _rules.Assess(TestData.ReleasableBatch(), DecisionKind.Release, TestData.QpSid);
        Assert.True(a.CanProceed, string.Join("; ", a.Findings));
    }

    [Fact]
    public void Reviewer_cannot_release_the_same_batch()
    {
        var a = _rules.Assess(TestData.ReleasableBatch(), DecisionKind.Release, TestData.ReviewerSid);
        Assert.Contains("BR061", Codes(a));
    }

    [Fact]
    public void Release_requires_a_review()
    {
        var batch = TestData.ReleasableBatch();
        batch.Decisions.Clear();
        Assert.Contains("BR060", Codes(_rules.Assess(batch, DecisionKind.Release, TestData.QpSid)));
    }

    [Fact]
    public void Final_batches_cannot_be_decided_again()
    {
        var batch = TestData.ReleasableBatch();
        batch.Status = BatchStatus.Released;
        Assert.Equal(new[] { "BR001" }, Codes(_rules.Assess(batch, DecisionKind.Reject, TestData.QpSid)));
    }

    [Fact]
    public void Open_step_blocks_review()
    {
        var batch = TestData.ReleasableBatch();
        batch.Steps[1].Status = StepStatus.Open;
        Assert.Contains("BR011", Codes(_rules.Assess(batch, DecisionKind.Review, TestData.ReviewerSid)));
    }

    [Fact]
    public void Self_verified_step_blocks_review()
    {
        var batch = TestData.ReleasableBatch();
        batch.Steps[0].VerifiedBy = "MLAHTI";
        Assert.Contains("BR013", Codes(_rules.Assess(batch, DecisionKind.Review, TestData.ReviewerSid)));
    }

    [Fact]
    public void Not_applicable_step_without_reason_is_a_warning()
    {
        var batch = TestData.ReleasableBatch();
        batch.Steps[2].Comment = null;
        var a = _rules.Assess(batch, DecisionKind.Release, TestData.QpSid);
        Assert.True(a.CanProceed);
        Assert.Contains(a.Findings, f => f.Code == "BR014" && f.Severity == FindingSeverity.Warning);
    }

    [Theory]
    [InlineData(null, false, "BR022")]
    [InlineData("OOS-26-031", false, "BR023")]
    [InlineData("OOS-26-031", true, "BR024")]
    public void Out_of_specification_result_blocks_release(string? investigation, bool closed, string code)
    {
        var batch = TestData.ReleasableBatch();
        batch.Results[0].Value = 94.94m;
        batch.Results[0].InvestigationNumber = investigation;
        batch.Results[0].InvestigationClosed = closed;
        Assert.Contains(code, Codes(_rules.Assess(batch, DecisionKind.Release, TestData.QpSid)));
    }

    [Fact]
    public void Pending_and_unapproved_results_block()
    {
        var batch = TestData.ReleasableBatch();
        batch.Results[0].Value = null;
        batch.Results[1].ApprovedBy = null;
        var codes = Codes(_rules.Assess(batch, DecisionKind.Review, TestData.ReviewerSid)).ToList();
        Assert.Contains("BR021", codes);
        Assert.Contains("BR025", codes);
    }

    [Fact]
    public void Open_major_deviation_blocks_but_assessed_minor_does_not()
    {
        var batch = TestData.ReleasableBatch();
        batch.Deviations.Add(new Deviation { Number = "DEV-26-0112", Classification = DeviationClass.Minor, Status = DeviationStatus.ImpactAssessed, Impact = ProductImpact.None });
        Assert.True(_rules.Assess(batch, DecisionKind.Release, TestData.QpSid).CanProceed);

        batch.Deviations.Add(new Deviation { Number = "DEV-26-0113", Classification = DeviationClass.Major, Status = DeviationStatus.ImpactAssessed, Impact = ProductImpact.None, CapaNumber = "CAPA-26-04" });
        Assert.Contains("BR031", Codes(_rules.Assess(batch, DecisionKind.Release, TestData.QpSid)));
    }

    [Fact]
    public void Unassessed_minor_deviation_warns_at_review_and_blocks_release()
    {
        var batch = TestData.ReleasableBatch();
        batch.Deviations.Add(new Deviation { Number = "DEV-26-0114", Classification = DeviationClass.Minor, Status = DeviationStatus.Open });
        batch.Decisions.Clear();
        batch.Status = BatchStatus.UnderReview;
        var review = _rules.Assess(batch, DecisionKind.Review, TestData.ReviewerSid);
        Assert.True(review.CanProceed);
        Assert.Contains(review.Findings, f => f.Code == "BR032" && f.Severity == FindingSeverity.Warning);

        batch.Decisions.Add(new Decision { Kind = DecisionKind.Review, SignerSid = TestData.ReviewerSid });
        Assert.Contains("BR032", Codes(_rules.Assess(batch, DecisionKind.Release, TestData.QpSid)));
    }

    [Fact]
    public void Confirmed_impact_blocks_even_when_closed()
    {
        var batch = TestData.ReleasableBatch();
        batch.Deviations.Add(new Deviation { Number = "DEV-26-0120", Classification = DeviationClass.Minor, Status = DeviationStatus.Closed, Impact = ProductImpact.Confirmed });
        Assert.Contains("BR030", Codes(_rules.Assess(batch, DecisionKind.Release, TestData.QpSid)));
    }

    [Fact]
    public void Expiry_beyond_registered_shelf_life_blocks()
    {
        var batch = TestData.ReleasableBatch();
        batch.ExpiresOn = new DateOnly(2029, 9, 30);
        Assert.Contains("BR041", Codes(_rules.Assess(batch, DecisionKind.Release, TestData.QpSid)));
    }

    [Fact]
    public void Too_little_remaining_shelf_life_blocks()
    {
        var rules = new DispositionRules(TestData.Clock(2028, 3, 1));
        Assert.Contains("BR042", Codes(rules.Assess(TestData.ReleasableBatch(), DecisionKind.Release, TestData.QpSid)));
    }

    [Fact]
    public void Missing_certificate_of_analysis_blocks()
    {
        var batch = TestData.ReleasableBatch();
        batch.Documents.RemoveAll(d => d.Kind == "CertificateOfAnalysis");
        Assert.Contains("BR050", Codes(_rules.Assess(batch, DecisionKind.Release, TestData.QpSid)));
    }

    [Fact]
    public void Hold_and_resume()
    {
        var batch = TestData.ReleasableBatch();
        Assert.True(_rules.Assess(batch, DecisionKind.Hold, TestData.QpSid).CanProceed);
        Assert.Contains("BR004", Codes(_rules.Assess(batch, DecisionKind.Resume, TestData.QpSid)));

        batch.Status = StatusTransitions.After(batch, DecisionKind.Hold);
        Assert.Equal(BatchStatus.OnHold, batch.Status);
        Assert.Contains("BR062", Codes(_rules.Assess(batch, DecisionKind.Release, TestData.QpSid)));
        Assert.Equal(BatchStatus.Reviewed, StatusTransitions.After(batch, DecisionKind.Resume));
    }

    [Fact]
    public void Batch_moves_to_review_when_required_documents_are_present()
    {
        var batch = TestData.ReleasableBatch();
        batch.Status = BatchStatus.Received;
        Assert.True(StatusTransitions.ReadyForReview(batch));
        batch.Documents.RemoveAt(0);
        Assert.False(StatusTransitions.ReadyForReview(batch));
    }
}
