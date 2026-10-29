using Proseware.ControlTower.Integration.Edifact;
using Xunit;

namespace Proseware.ControlTower.Tests.Integration;

public class EdifactReaderTests
{
    [Fact]
    public void Parses_iftmin_sample_with_una_and_latin1_text()
    {
        var interchange = EdifactReader.Parse(Samples.Edifact("IFTMIN_6430012345000_26102907120001.edi"));

        Assert.Equal("6430012345000", interchange.SenderId);
        Assert.Equal("14", interchange.SenderQualifier);
        Assert.Equal("003712345678", interchange.RecipientId);
        Assert.Equal("26102907120001", interchange.ControlReference);
        Assert.Equal(new DateTime(2026, 10, 29, 7, 12, 0), interchange.Prepared);
        var message = Assert.Single(interchange.Messages);
        Assert.Equal("IFTMIN", message.Type);
        Assert.Equal("96A", message.Release);
        Assert.Equal(4, message.All("GIN").Count());
        var consignor = message.All("NAD").First(n => n.Get(0) == "CZ");
        Assert.Equal("Hatanpään valtatie 30", consignor.Get(4));
    }

    [Fact]
    public void Release_character_keeps_separators_in_text()
    {
        var interchange = EdifactReader.Parse(Samples.Edifact("IFTSTA_003712345678_NRL2610291532.edi"));
        var ftx = interchange.Messages[0].All("FTX").Last();
        Assert.Equal("Driver waiting for ramp: 40 min", ftx.Get(3));
    }

    [Fact]
    public void Default_separators_are_used_without_una()
    {
        var interchange = EdifactReader.Parse(Samples.Edifact("IFTSTA_OCNXEDI_OC88213.edi"));
        Assert.Equal("OCNXEDI", interchange.SenderId);
        Assert.Equal("D", interchange.Messages[0].Version);
        Assert.Equal("01B", interchange.Messages[0].Release);
        Assert.Equal(new DateTime(2026, 10, 29, 4, 5, 0), interchange.Prepared);
    }

    [Fact]
    public void Wrong_segment_count_is_rejected()
    {
        var text = Samples.Edifact("CONTRL_003712345678_NRL2610290713.edi").Replace("UNT+3+1'", "UNT+4+1'");
        var ex = Assert.Throws<EdifactException>(() => EdifactReader.Parse(text));
        Assert.Contains("segment count", ex.Message);
    }

    [Fact]
    public void Missing_unz_is_rejected()
    {
        var text = Samples.Edifact("CONTRL_003712345678_NRL2610290713.edi");
        text = text[..text.IndexOf("UNZ", StringComparison.Ordinal)];
        Assert.Throws<EdifactException>(() => EdifactReader.Parse(text));
    }

    [Theory]
    [InlineData("20261029", "102", 2026, 10, 29, 0, 0, 0)]
    [InlineData("202610291532", "203", 2026, 10, 29, 15, 32, 0)]
    [InlineData("202610281930+02", "303", 2026, 10, 28, 19, 30, 2)]
    public void Parses_dtm_formats(string value, string format, int y, int mo, int d, int h, int mi, int offset)
    {
        var parsed = EdifactReader.ParseDtm(value, format);
        Assert.Equal(new DateTimeOffset(y, mo, d, h, mi, 0, TimeSpan.FromHours(offset)), parsed);
    }
}
