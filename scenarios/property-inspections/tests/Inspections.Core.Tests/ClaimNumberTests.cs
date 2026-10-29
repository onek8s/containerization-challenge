using System;
using Northwind.Inspections.Core;
using Xunit;

namespace Northwind.Inspections.Core.Tests
{
    public class ClaimNumberTests
    {
        [Theory]
        [InlineData("HO-2024-004512", "HO-2024-004512")]
        [InlineData("ho2024004512", "HO-2024-004512")]
        [InlineData(" co-2025 000017 ", "CO-2025-000017")]
        public void Normalizes(string input, string expected)
        {
            Assert.Equal(expected, ClaimNumber.Normalize(input));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("XX-2024-004512")]
        [InlineData("HO-1999-004512")]
        [InlineData("HO-2024-4512")]
        public void RejectsInvalid(string input)
        {
            string normalized;
            Assert.False(ClaimNumber.TryNormalize(input, out normalized));
            Assert.Throws<FormatException>(() => ClaimNumber.Normalize(input));
        }

        [Fact]
        public void ReturnsYear()
        {
            Assert.Equal(2026, ClaimNumber.Year("fl-2026-000001"));
        }
    }
}
