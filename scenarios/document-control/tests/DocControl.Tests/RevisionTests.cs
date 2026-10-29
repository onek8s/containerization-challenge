using Litware.DocControl.Documents;

namespace Litware.DocControl.Tests;

public class RevisionTests
{
    private static (RevisionCode, RevisionStatus) R(string code, RevisionStatus status) => (RevisionCode.Parse(code), status);

    [Fact]
    public void Orders_preliminary_before_construction()
    {
        Assert.True(RevisionCode.Parse("P99") < RevisionCode.Parse("C01"));
        Assert.True(RevisionCode.Parse("P02") > RevisionCode.Parse("P01"));
        Assert.Equal("C07", RevisionCode.Parse("c07").ToString());
    }

    [Theory]
    [InlineData("A")]
    [InlineData("P00")]
    [InlineData("X01")]
    [InlineData("P1")]
    public void Rejects_invalid_codes(string text) => Assert.False(RevisionCode.TryParse(text, out _));

    [Fact]
    public void First_revision_must_be_P01_or_C01()
    {
        Assert.Null(RevisionRules.CheckNewRevision(RevisionCode.Parse("P01"), []));
        Assert.Null(RevisionRules.CheckNewRevision(RevisionCode.Parse("C01"), []));
        Assert.NotNull(RevisionRules.CheckNewRevision(RevisionCode.Parse("P02"), []));
    }

    [Fact]
    public void Next_revision_follows_highest()
    {
        var history = new[] { R("P01", RevisionStatus.Superseded), R("P02", RevisionStatus.Approved) };
        Assert.Null(RevisionRules.CheckNewRevision(RevisionCode.Parse("P03"), history));
        Assert.Null(RevisionRules.CheckNewRevision(RevisionCode.Parse("C01"), history));
        Assert.Equal("The next revision is P03.", RevisionRules.CheckNewRevision(RevisionCode.Parse("P04"), history));
        Assert.Equal(RevisionCode.Parse("P03"), RevisionRules.SuggestNext(history));
    }

    [Fact]
    public void Cannot_go_back_to_preliminary()
    {
        var history = new[] { R("C01", RevisionStatus.Approved) };
        Assert.NotNull(RevisionRules.CheckNewRevision(RevisionCode.Parse("P05"), history));
    }

    [Fact]
    public void Blocks_while_previous_is_in_review()
    {
        var history = new[] { R("P01", RevisionStatus.Submitted) };
        Assert.Equal("The previous revision is still waiting for review.", RevisionRules.CheckNewRevision(RevisionCode.Parse("P02"), history));
    }

    [Fact]
    public void Rejected_revision_can_be_resubmitted()
    {
        var history = new[] { R("P01", RevisionStatus.Approved), R("P02", RevisionStatus.Rejected) };
        Assert.Null(RevisionRules.CheckNewRevision(RevisionCode.Parse("P02"), history));
    }

    [Theory]
    [InlineData("P1042-LIT-ME-DRG-0012_P03.pdf", true)]
    [InlineData("P1042-LIT-ME-DRG-0012_C01.DWG", true)]
    [InlineData("P1042-LIT-ME-DRG-0012.pdf", false)]
    [InlineData("P1042-LIT-ME-DRG-0012_P03.exe", false)]
    [InlineData("P1042-LIT-ME-DRG-0012_Z03.pdf", false)]
    public void Parses_revision_file_names(string name, bool valid)
    {
        Assert.Equal(valid, RevisionFileName.TryParse(name, out var f, out var error));
        if (valid) Assert.Equal(name.ToLowerInvariant(), f!.ToString().ToLowerInvariant());
        else Assert.NotNull(error);
    }
}
