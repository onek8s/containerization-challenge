using Litware.DocControl.Documents;
using Litware.DocControl.Transmittals;

namespace Litware.DocControl.Tests;

public class TransmittalTests
{
    [Fact]
    public void Formats_numbers()
    {
        Assert.Equal("TR-P1042-00017", TransmittalNumber.Format("P1042", 17));
        Assert.True(TransmittalNumber.IsValid("TR-P1042-00017"));
        Assert.False(TransmittalNumber.IsValid("TR-1042-17"));
    }

    [Fact]
    public void Submitted_revisions_only_for_review()
    {
        var lines = new[] { new TransmittalLine("P1042-LIT-PI-PID-0003", "P01", "P&ID", RevisionStatus.Submitted, "P1042") };
        Assert.Empty(TransmittalRules.Validate("P1042", TransmittalPurpose.ForReview, lines));
        Assert.NotEmpty(TransmittalRules.Validate("P1042", TransmittalPurpose.ForApproval, lines));
    }

    [Fact]
    public void Construction_issue_needs_C_revisions()
    {
        var lines = new[] { new TransmittalLine("P1042-LIT-ME-DRG-0012", "P02", "GA", RevisionStatus.Approved, "P1042") };
        var errors = TransmittalRules.Validate("P1042", TransmittalPurpose.ForConstruction, lines);
        Assert.Contains(errors, e => e.Contains("preliminary"));
    }

    [Fact]
    public void Rejects_other_project_and_duplicates()
    {
        var lines = new[]
        {
            new TransmittalLine("P1057-LIT-ST-CAL-0004", "C01", "Calc", RevisionStatus.Approved, "P1057"),
            new TransmittalLine("P1057-LIT-ST-CAL-0004", "C01", "Calc", RevisionStatus.Approved, "P1057"),
        };
        var errors = TransmittalRules.Validate("P1042", TransmittalPurpose.ForInformation, lines);
        Assert.Contains(errors, e => e.Contains("more than once"));
        Assert.Contains(errors, e => e.Contains("belongs to project"));
    }
}
