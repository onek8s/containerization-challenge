using Tailspin.CustomerCare.Domain;
using Xunit;

namespace Tailspin.CustomerCare.Domain.Tests
{
    public class CaseNumberTests
    {
        [Fact]
        public void Format_pads_sequence()
        {
            Assert.Equal("CC-2026-004512", CaseNumber.Format(2026, 4512));
        }

        [Theory]
        [InlineData("CC-2026-004512", true)]
        [InlineData("cc-2026-004512", true)]
        [InlineData("CC-2026-4512", false)]
        [InlineData("CC-2026-000000", false)]
        [InlineData("", false)]
        public void TryParse(string value, bool expected)
        {
            int y, s;
            Assert.Equal(expected, CaseNumber.TryParse(value, out y, out s));
        }

        [Theory]
        [InlineData("4512", "CC-2026-004512")]
        [InlineData("cc-2025-77", "CC-2025-000077")]
        [InlineData("CC4512", "CC-2026-004512")]
        [InlineData("CC-2026-004512", "CC-2026-004512")]
        [InlineData("abc", null)]
        [InlineData("0", null)]
        public void Normalize(string input, string expected)
        {
            Assert.Equal(expected, CaseNumber.Normalize(input, 2026));
        }
    }
}
