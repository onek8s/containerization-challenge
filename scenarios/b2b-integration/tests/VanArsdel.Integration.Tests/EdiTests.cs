using System.Xml;
using VanArsdel.Integration.Edi;
using VanArsdel.Integration.Edi.Acks;
using VanArsdel.Integration.Edi.Envelopes;
using Xunit;

namespace VanArsdel.Integration.Tests
{
    public class EdiTests
    {
        [Fact]
        public void X12_850_sample_has_two_valid_sets()
        {
            var result = EdiDisassembler.Disassemble(TestLab.SampleText("C1001_850_HW-55012.x12"));

            Assert.False(result.HasEnvelopeErrors);
            Assert.Equal(EdiStandard.X12, result.Interchange.Header.Standard);
            Assert.Equal("HARTWELL", result.Interchange.Header.SenderId);
            Assert.Equal("000004711", result.Interchange.Header.ControlNumber);
            Assert.Equal(2, result.Accepted.Count());
            Assert.All(result.Transactions, t => Assert.Equal("850", t.Set.Type));
        }

        [Fact]
        public void Edifact_orders_sample_is_parsed_with_una_delimiters()
        {
            var result = EdiDisassembler.Disassemble(TestLab.SampleText("C2001_ORDERS_BW-778812.edi"));

            Assert.Equal(EdiStandard.Edifact, result.Interchange.Header.Standard);
            Assert.Equal("4012345000009", result.Interchange.Header.SenderId);
            Assert.True(result.Interchange.Header.AckRequested);
            var orders = Assert.Single(result.Accepted);
            Assert.Equal("ORDERS", orders.Set.Type);
        }

        [Fact]
        public void Wrong_segment_count_rejects_the_set()
        {
            var text = TestLab.SampleText("C1002_850_BF-2026-0311.x12").Replace("SE*12*0001", "SE*11*0001");

            var result = EdiDisassembler.Disassemble(text);

            var set = Assert.Single(result.Rejected);
            Assert.Contains(set.Set.Errors, e => e.ToString().Contains("count", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void Ack_for_an_x12_interchange_is_a_997_per_group()
        {
            var result = EdiDisassembler.Disassemble(TestLab.SampleText("C1001_850_HW-55012.x12"));

            var acks = AckBuilder.Build(result);

            var ack = Assert.Single(acks);
            Assert.Contains("997", ack.DocumentElement.LocalName, StringComparison.Ordinal);
            Assert.Contains("4711", ack.OuterXml, StringComparison.Ordinal);
        }

        [Fact]
        public void Built_interchange_round_trips_through_the_parser()
        {
            var source = EdiDisassembler.Disassemble(TestLab.SampleText("C1002_850_BF-2026-0311.x12"));
            var header = new InterchangeHeader
            {
                Standard = EdiStandard.X12,
                SenderQualifier = "ZZ",
                SenderId = "VANARSDEL",
                ReceiverQualifier = "01",
                ReceiverId = "078123456",
                ControlNumber = "000001234",
                Prepared = new DateTime(2026, 3, 2, 10, 15, 0),
                Version = "00401",
                AckRequested = true
            };

            var text = new InterchangeBuilder(header).Build(new List<XmlDocument> { source.Transactions[0].Document }, "1234");
            var again = EdiDisassembler.Disassemble(text);

            Assert.False(again.HasEnvelopeErrors);
            Assert.Equal("000001234", again.Interchange.Header.ControlNumber);
            Assert.Equal("1234", again.Interchange.Groups[0].ControlNumber);
            Assert.Single(again.Accepted);
        }
    }
}
