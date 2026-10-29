using System.Text;
using VanArsdel.Integration.Domain.Codes;
using VanArsdel.Integration.Domain.Routing;
using VanArsdel.Integration.Messaging;
using Xunit;

namespace VanArsdel.Integration.Tests
{
    public class MessagingTests
    {
        [Fact]
        public void Encoding_fixer_rereads_ansi_and_drops_control_characters()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var ansi = Encoding.GetEncoding(1252).GetBytes("Kabel NYM-J 3x1,5 mm² für Außenbereich\0\0");

            var fixedBody = EncodingFixer.Fix(ansi, new EncodingFixerSettings { FallbackCodePage = 1252, RemoveControlCharacters = true });

            Assert.Equal("Kabel NYM-J 3x1,5 mm² für Außenbereich", Encoding.UTF8.GetString(fixedBody.Body));
            Assert.Equal(2, fixedBody.RemovedCharacters);
        }

        [Fact]
        public void Duplicate_detector_sees_the_same_body_within_the_window()
        {
            var detector = new DuplicateDetector(new InMemoryDuplicateStore(), TimeSpan.FromHours(72));
            var now = new DateTime(2026, 3, 2, 8, 0, 0, DateTimeKind.Utc);

            Assert.False(detector.IsDuplicate("C1001", null, "abc", now));
            Assert.True(detector.IsDuplicate("C1001", null, "abc", now.AddHours(1)));
            Assert.False(detector.IsDuplicate("C1002", null, "abc", now.AddHours(1)));
        }

        [Fact]
        public void Debatcher_splits_a_gateway_batch_and_keeps_envelope_attributes()
        {
            using var stream = new MemoryStream(TestLab.Sample("C3001_OrderBatch_RW-20260302-01.xml"));

            var parts = XmlDebatcher.Split(stream, "Order");

            Assert.Equal(3, parts.Count);
            Assert.All(parts, p => Assert.Equal("C3001", p.EnvelopeAttributes["PartnerId"]));
            Assert.Contains("RW-0302-02", Encoding.UTF8.GetString(parts[1].Body), StringComparison.Ordinal);
        }

        [Fact]
        public void Routing_slip_advances_to_completion()
        {
            var slip = RoutingSlip.Parse("0|MAP:Despatch_to_X12_00401_856;SEND:AS2.C1001;ARCHIVE:outbound;TRACK:Despatch");

            Assert.Equal(RoutingStepKind.Transform, slip.Current.Kind);
            slip = slip.Advance();
            Assert.Equal("AS2.C1001", slip.Current.Target);
            slip = slip.Advance().Advance().Advance();
            Assert.True(slip.IsComplete);
            Assert.StartsWith("4|", slip.ToString(), StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("00614141100200", true)]
        [InlineData("00614141100201", false)]
        [InlineData("4012345000009", true)]
        public void Gtin_and_gln_check_digits(string code, bool valid)
        {
            Assert.Equal(valid, CheckDigit.IsValid(code));
        }

        [Fact]
        public void Sscc_has_company_prefix_and_valid_check_digit()
        {
            var sscc = Sscc.Create(0, 2600001);

            Assert.Equal(18, sscc.Length);
            Assert.StartsWith("00614141", sscc, StringComparison.Ordinal);
            Assert.True(Sscc.IsValid(sscc));
        }
    }
}
