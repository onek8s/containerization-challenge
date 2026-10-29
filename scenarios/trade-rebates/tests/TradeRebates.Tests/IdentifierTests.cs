using ContosoFoods.TradeRebates.Rules;

namespace TradeRebates.Tests;

public class IdentifierTests
{
    [Theory]
    [InlineData("6411401015090", true)]
    [InlineData("06411401015090", true)]
    [InlineData("96385074", true)]
    [InlineData("036000291452", true)]
    [InlineData("6411401015091", false)]
    [InlineData("64114010150", false)]
    [InlineData("64114O1015090", false)]
    [InlineData(null, false)]
    public void Gtin_check_digit(string? gtin, bool valid)
    {
        Assert.Equal(valid, Gtin.IsValid(gtin));
    }

    [Fact]
    public void Gtin_is_padded_to_14()
    {
        Assert.Equal("06411401015090", Gtin.ToGtin14("6411401015090"));
        Assert.Equal("00000096385074", Gtin.ToGtin14("96385074"));
    }

    [Theory]
    [InlineData("FI21 1234 5600 0007 85", true)]
    [InlineData("fi2112345600000785", true)]
    [InlineData("DE89 3704 0044 0532 0130 00", true)]
    [InlineData("SE45 5000 0000 0583 9825 7466", true)]
    [InlineData("FI21 1234 5600 0007 86", false)]
    [InlineData("FI21", false)]
    [InlineData("1121 1234 5600 0007 85", false)]
    [InlineData(null, false)]
    public void Iban_checksum(string? iban, bool valid)
    {
        Assert.Equal(valid, Iban.IsValid(iban));
    }

    [Fact]
    public void Iban_mask_keeps_country_and_last_four()
    {
        Assert.Equal("FI************0785", Iban.Mask("FI21 1234 5600 0007 85"));
        Assert.Equal("****", Iban.Mask("FI21"));
    }
}
