using ContosoFoods.TradeRebates.Rules;

namespace TradeRebates.Tests;

public class TierScheduleTests
{
    [Fact]
    public void Parses_and_formats_round_trip()
    {
        var s = TierSchedule.Parse("R;0:0;10000:1.5;25000:2.25");
        Assert.Equal(TierMode.Retroactive, s.Mode);
        Assert.Equal(3, s.Tiers.Count);
        Assert.Equal("R;0:0;10000:1.5;25000:2.25", s.ToString());
    }

    [Theory]
    [InlineData("STEPPED; 0:1 ; 500:2%", "S;0:1;500:2")]
    [InlineData("retro;0:0.5", "R;0:0.5")]
    public void Accepts_long_mode_names_spaces_and_percent(string text, string expected)
    {
        Assert.Equal(expected, TierSchedule.Parse(text).ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("X;0:1")]
    [InlineData("R")]
    [InlineData("R;100:1")]
    [InlineData("R;0:1;500:2;400:3")]
    [InlineData("R;0:1;500")]
    [InlineData("R;0:101")]
    [InlineData("R;0:abc")]
    public void Rejects_invalid_text(string text)
    {
        Assert.False(TierSchedule.TryParse(text, out _, out string? error));
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(9999.99, 0)]
    [InlineData(10000, 1.5)]
    [InlineData(24999, 1.5)]
    [InlineData(25000, 2.25)]
    [InlineData(1000000, 2.25)]
    public void Rate_follows_highest_tier_reached(decimal measure, decimal rate)
    {
        var s = TierSchedule.Parse("R;0:0;10000:1.5;25000:2.25");
        Assert.Equal(rate, s.RateFor(measure));
    }

    [Fact]
    public void Retroactive_applies_reached_rate_to_whole_value()
    {
        var s = TierSchedule.Parse("R;0:0;10000:1.5;25000:2.25");
        Assert.Equal(675.00m, s.RebateFor(30000m, 30000m));
        Assert.Equal(0m, s.RebateFor(9000m, 9000m));
    }

    [Fact]
    public void Stepped_rebates_each_band_at_its_own_rate()
    {
        var s = TierSchedule.Parse("S;0:0;10000:1.5;25000:2.25");
        // 10000 * 0 + 15000 * 1.5% + 5000 * 2.25% = 225 + 112.5
        Assert.Equal(337.50m, s.RebateFor(30000m, 30000m));
    }

    [Fact]
    public void Stepped_volume_basis_weights_rate_by_volume_share()
    {
        var s = TierSchedule.Parse("S;0:1;1000:3");
        // 2000 units: half at 1%, half at 3% => 2% of value 50000
        Assert.Equal(1000.00m, s.RebateFor(2000m, 50000m));
        Assert.Equal(2.0000m, s.EffectiveRate(2000m, 50000m));
    }

    [Fact]
    public void Credit_periods_give_no_rebate()
    {
        var s = TierSchedule.Parse("R;0:2");
        Assert.Equal(0m, s.RebateFor(-10m, -500m));
        Assert.Equal(0m, s.RebateFor(10m, 0m));
    }

    [Fact]
    public void Distance_to_next_tier()
    {
        var s = TierSchedule.Parse("R;0:0;10000:1.5;25000:2.25");
        Assert.Equal(2500m, s.ToNextTier(7500m));
        Assert.Equal(15000m, s.ToNextTier(10000m));
        Assert.Null(s.ToNextTier(30000m));
    }

    [Fact]
    public void Binary_form_round_trips()
    {
        var s = TierSchedule.Parse("S;0:0.5;1500.5:1.25;9000:3.125");
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
            s.Write(w);

        // version + mode + count + 3 * (16 + 16)
        Assert.Equal(3 + 3 * 32, ms.Length);
        ms.Position = 0;
        using var r = new BinaryReader(ms);
        var back = TierSchedule.Read(r);
        Assert.Equal(s.ToString(), back.ToString());
        Assert.Equal(TierMode.Stepped, back.Mode);
    }

    [Fact]
    public void Binary_form_starts_with_version_and_mode_byte()
    {
        var s = TierSchedule.Parse("R;0:1");
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms))
            s.Write(w);
        byte[] bytes = ms.ToArray();
        Assert.Equal(1, bytes[0]);
        Assert.Equal((byte)'R', bytes[1]);
        Assert.Equal(1, bytes[2]);
    }

    [Fact]
    public void Rejects_unknown_binary_version()
    {
        using var ms = new MemoryStream(new byte[] { 9, (byte)'R', 0 });
        using var r = new BinaryReader(ms);
        Assert.Throws<InvalidDataException>(() => TierSchedule.Read(r));
    }
}
