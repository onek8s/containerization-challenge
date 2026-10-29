using Northwind.Inspections.Core;
using Xunit;

namespace Northwind.Inspections.Core.Tests
{
    public class PasswordHasherTests
    {
        [Fact]
        public void VerifiesOwnHash()
        {
            var hash = PasswordHasher.Hash("Survey!2024");
            Assert.StartsWith("PBKDF2$10000$", hash);
            Assert.True(PasswordHasher.Verify("Survey!2024", hash));
            Assert.False(PasswordHasher.Verify("survey!2024", hash));
        }

        [Fact]
        public void SaltsEachHash()
        {
            Assert.NotEqual(PasswordHasher.Hash("same"), PasswordHasher.Hash("same"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("plain")]
        [InlineData("PBKDF2$10$AAAA$AAAA")]
        [InlineData("PBKDF2$10000$!!$AAAA")]
        public void RejectsMalformedHashes(string stored)
        {
            Assert.False(PasswordHasher.Verify("x", stored));
        }
    }
}
