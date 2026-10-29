using Proseware.ControlTower.Domain.Shipments;
using Xunit;

namespace Proseware.ControlTower.Tests.Domain;

public class IdentifierTests
{
    [Fact]
    public void Shipment_numbers_carry_a_luhn_check_digit()
    {
        var number = ShipmentNumber.Format(2026, 3);
        Assert.Equal("PF26-0000039", number);
        Assert.True(ShipmentNumber.IsValid(number));
        Assert.False(ShipmentNumber.IsValid("PF26-0000038"));
        Assert.False(ShipmentNumber.IsValid("PF26-000003"));
        Assert.False(ShipmentNumber.IsValid(null));
    }

    [Theory]
    [InlineData("364300123450003008")]
    [InlineData("(00)364300123450003008")]
    [InlineData("00364300123450003008")]
    [InlineData("]C100364300123450003008")]
    public void Sscc_accepts_scanner_formats(string raw)
    {
        Assert.True(Sscc.IsValid(raw));
        Assert.Equal("364300123450003008", Sscc.Normalize(raw));
    }

    [Fact]
    public void Sscc_create_computes_gs1_check_digit()
    {
        // GS1 example SSCC 1 0614141 123456789 7
        Assert.Equal(7, Sscc.CheckDigit("10614141123456789"));
        var sscc = Sscc.Create('3', Sscc.DefaultCompanyPrefix, 300);
        Assert.Equal("364300123450003008", sscc);
        Assert.False(Sscc.IsValid("364300123450003009"));
    }
}
