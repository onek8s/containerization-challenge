using Litware.DocControl.Documents;

namespace Litware.DocControl.Tests;

public class DocumentNumberTests
{
    [Theory]
    [InlineData("P1042-LIT-ME-DRG-0012")]
    [InlineData("p1042-lit-pi-pid-0003")]
    [InlineData(" P1057-LIT-ST-CAL-0004 ")]
    public void Parses_valid_numbers(string text)
    {
        Assert.True(DocumentNumber.TryParse(text, out var n));
        Assert.Equal(text.Trim().ToUpperInvariant(), n!.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("P1042-LIT-XX-DRG-0012")]   // unknown discipline
    [InlineData("P1042-LIT-ME-ABC-0012")]   // unknown type
    [InlineData("P1042-LIT-ME-DRG-0000")]   // sequence zero
    [InlineData("P104-LIT-ME-DRG-0012")]
    [InlineData("P1042-LITW-ME-DRG-0012")]
    public void Rejects_invalid_numbers(string text) => Assert.False(DocumentNumber.TryParse(text, out _));

    [Fact]
    public void Splits_parts()
    {
        var n = DocumentNumber.Parse("P1042-LIT-EL-LST-0001");
        Assert.Equal("P1042", n.Project);
        Assert.Equal("EL", n.Discipline);
        Assert.Equal("LST", n.DocumentType);
        Assert.Equal(1, n.Sequence);
    }
}
