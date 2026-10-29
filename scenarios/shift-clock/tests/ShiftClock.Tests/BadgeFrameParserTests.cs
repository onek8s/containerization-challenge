using ShiftClock.Shared;

namespace ShiftClock.Tests;

public class BadgeFrameParserTests
{
    [Fact]
    public void Decodes_frame_built_for_badge()
    {
        var parser = new BadgeFrameParser();

        var badges = parser.Feed(BadgeFrameParser.BuildFrame("0F00A1B2C3"));

        Assert.Equal(["0F00A1B2C3"], badges);
        Assert.Equal(0, parser.RejectedFrames);
    }

    [Fact]
    public void Handles_frames_split_across_reads()
    {
        var parser = new BadgeFrameParser();
        var frame = BadgeFrameParser.BuildFrame("1234567890");

        Assert.Empty(parser.Feed(frame.AsSpan(0, 5)));
        Assert.Equal(["1234567890"], parser.Feed(frame.AsSpan(5)));
    }

    [Fact]
    public void Reads_several_frames_in_one_buffer_and_ignores_noise()
    {
        var parser = new BadgeFrameParser();
        byte[] data = [0x00, 0xFF, .. BadgeFrameParser.BuildFrame("AAAAAAAAAA"), 0x0D, .. BadgeFrameParser.BuildFrame("0000000001")];

        Assert.Equal(["AAAAAAAAAA", "0000000001"], parser.Feed(data));
    }

    [Fact]
    public void Rejects_bad_checksum()
    {
        var parser = new BadgeFrameParser();
        var frame = BadgeFrameParser.BuildFrame("0F00A1B2C3");
        frame[11] = (byte)'0';
        frame[12] = (byte)'0';

        Assert.Empty(parser.Feed(frame));
        Assert.Equal(1, parser.RejectedFrames);
    }

    [Fact]
    public void Rejects_overlong_frame()
    {
        var parser = new BadgeFrameParser();
        byte[] data = [BadgeFrameParser.Stx, .. "0123456789ABCDEF0123"u8.ToArray(), BadgeFrameParser.Etx];

        Assert.Empty(parser.Feed(data));
        Assert.Equal(1, parser.RejectedFrames);
    }
}
