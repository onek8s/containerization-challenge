using Lucerne.Lending.CoreBanking;
using Xunit;

namespace Lucerne.Lending.Tests;

/// <summary>Parameter lengths must match the RPG data structures in legacy/core/qrpglesrc.</summary>
public class CoreLayoutTests
{
    [Theory]
    [InlineData("CUSTINQ.IN", 21)]
    [InlineData("CUSTINQ.OUT", 256)]
    [InlineData("CUSCRT.IN", 242)]
    [InlineData("CUSCRT.OUT", 90)]
    [InlineData("LNBOOK.IN", 97)]
    [InlineData("LNBOOK.OUT", 92)]
    [InlineData("LNDISB.IN", 133)]
    [InlineData("LNDISB.OUT", 101)]
    [InlineData("LNFEE.IN", 61)]
    [InlineData("LNPPLN.IN", 192)]
    [InlineData("LNTERM.IN", 30)]
    [InlineData("RESULT.OUT", 80)]
    [InlineData("LNEVTQ", 64)]
    public void Layout_lengths(string name, int length)
    {
        var layout = All().Single(l => l.Name == name);
        Assert.Equal(length, layout.Length);
    }

    [Fact]
    public void Packed_fields_use_half_bytes()
    {
        Assert.Equal(7, CoreLayouts.LnBookIn["AMOUNT"].ByteLength);
        Assert.Equal(4, CoreLayouts.LnBookIn["RATE"].ByteLength);
        Assert.Equal(44, CoreLayouts.LnBookIn.OffsetOf("AMOUNT"));
        Assert.Equal(24, CoreLayouts.LnPplnIn.OffsetOf("DUE01"));
        Assert.Equal(178, CoreLayouts.LnPplnIn.OffsetOf("DUE12"));
    }

    [Fact]
    public void Return_codes_are_described() =>
        Assert.Equal("Record locked by another job", CoreReturnCodes.Describe(CoreReturnCodes.Locked));

    private static IEnumerable<RecordLayout> All() => typeof(CoreLayouts).GetFields()
        .Where(f => f.FieldType == typeof(RecordLayout)).Select(f => (RecordLayout)f.GetValue(null)!);
}
