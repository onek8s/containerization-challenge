using VanArsdel.Integration.Domain.Orders;
using VanArsdel.Integration.Domain.Sla;
using VanArsdel.Integration.LabStubs.EndToEnd;
using VanArsdel.Integration.LabStubs.Simulators;
using VanArsdel.Integration.Monitoring;
using Xunit;

namespace VanArsdel.Integration.Tests
{
    public class LabTests
    {
        [Fact]
        public void Mdn_carries_the_mic_of_the_payload()
        {
            var body = TestLab.Sample("C1001_850_HW-55012.x12");

            var receipt = As2.ParseMdn(As2.Mdn("<1@hartwell>", "HARTWELL-AS2", As2.OurAs2Id, body, null));

            Assert.True(receipt.Processed);
            Assert.Equal(As2.Mic(body), receipt.ReceivedMic);
        }

        [Fact]
        public void Sla_evaluator_raises_overdue_acknowledgements_only()
        {
            var now = new DateTime(2026, 3, 2, 12, 0, 0, DateTimeKind.Utc);
            var snapshot = new MonitoringSnapshot { TakenUtc = now };
            snapshot.OutboundAwaitingAck.Add(new InterchangeRecord { PartnerId = "C1001", ControlNumber = "000001002", TransactionType = "856", AckStatus = AckStatus.Pending, CreatedUtc = now.AddHours(-5), AckDueUtc = now.AddHours(-1) });
            snapshot.OutboundAwaitingAck.Add(new InterchangeRecord { PartnerId = "C1002", ControlNumber = "000001003", TransactionType = "856", AckStatus = AckStatus.Pending, CreatedUtc = now.AddHours(-1), AckDueUtc = now.AddHours(7) });
            var definition = new SlaDefinition { Code = "ACK-OVERDUE", Kind = SlaKind.AckOverdue, Severity = AlertSeverity.Warning, NotifyGroup = "Integration", Enabled = true };

            var alerts = new SlaEvaluator(BusinessCalendar.Default).Evaluate(snapshot, new[] { definition });

            var alert = Assert.Single(alerts);
            Assert.Equal("C1001", alert.PartnerId);
        }

        [Fact]
        public async Task End_to_end_run_passes_all_checks()
        {
            var work = Path.Combine(Path.GetTempPath(), "b2b-tests", "e2e-" + Guid.NewGuid().ToString("N"));
            try
            {
                Assert.Equal(0, await new EndToEndRunner().RunAsync(work));
            }
            finally
            {
                Directory.Delete(work, true);
            }
        }
    }
}
