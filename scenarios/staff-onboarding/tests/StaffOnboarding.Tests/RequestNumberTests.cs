using StaffOnboarding.Core.Requests;

namespace StaffOnboarding.Tests;

public class RequestNumberTests
{
    [Theory]
    [InlineData(RequestKind.Joiner, 2026, 42, "ONB-2026-00042")]
    [InlineData(RequestKind.Mover, 2027, 7, "MOV-2027-00007")]
    [InlineData(RequestKind.Leaver, 2026, 99999, "LVR-2026-99999")]
    public void Formats_and_parses(RequestKind kind, int year, int sequence, string expected)
    {
        Assert.Equal(expected, RequestNumber.Format(kind, year, sequence));
        Assert.True(RequestNumber.TryParse(expected.ToLowerInvariant(), out var k, out var y, out var s));
        Assert.Equal((kind, year, sequence), (k, y, s));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("ONB-26-1")]
    [InlineData("XYZ-2026-00001")]
    [InlineData("ONB-2026-00000")]
    public void Rejects_invalid(string? value)
    {
        Assert.False(RequestNumber.TryParse(value, out _, out _, out _));
    }
}
