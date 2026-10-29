using System;
using System.IO;
using System.Linq;
using ProductReturns.Core;
using Xunit;

namespace ProductReturns.Tests
{
    public class RmaAndReceivingTests
    {
        [Fact]
        public void Rma_numbers_round_trip_and_validate()
        {
            var rma = RmaNumber.Create(4711, new DateTime(2024, 10, 3));
            Assert.Matches(@"^R2410-004711-\d$", rma);
            Assert.True(RmaNumber.IsValid(rma));
            Assert.True(RmaNumber.IsValid(rma.Replace("-", string.Empty).ToLowerInvariant()));
            Assert.Equal(4711, RmaNumber.IdOf(rma));

            var wrong = rma.Substring(0, rma.Length - 1) + ((rma[rma.Length - 1] - '0' + 1) % 10);
            Assert.False(RmaNumber.IsValid(wrong));
            Assert.False(RmaNumber.IsValid("SO-1048213"));
            Assert.False(RmaNumber.IsValid(null));
        }

        [Fact]
        public void Large_ids_keep_working()
        {
            var rma = RmaNumber.Create(12345678, new DateTime(2025, 1, 1));
            Assert.True(RmaNumber.IsValid(rma));
            Assert.Equal(12345678, RmaNumber.IdOf(rma));
        }

        private static ReturnRequest Approved()
        {
            var request = new ReturnRequest { RmaNumber = "R2410-000001-0", Status = ReturnStatus.LabelIssued };
            request.Lines.Add(new ReturnLine { Sku = "A", Quantity = 2 });
            request.Lines.Add(new ReturnLine { Sku = "B", Quantity = 1 });
            return request;
        }

        [Fact]
        public void Receiving_tracks_quantities_and_worst_condition()
        {
            var request = Approved();
            var now = DateTime.UtcNow;
            Assert.True(ReceivingRules.Receive(request, "a", 1, ItemCondition.AsNew, now).Accepted);
            Assert.Equal(ReturnStatus.Received, request.Status);
            var second = ReceivingRules.Receive(request, "A", 1, ItemCondition.Used, now);
            Assert.True(second.Accepted);
            Assert.False(second.Complete);
            Assert.Equal(ItemCondition.Used, request.Lines[0].Condition);

            Assert.False(ReceivingRules.Receive(request, "A", 1, ItemCondition.AsNew, now).Accepted);
            Assert.False(ReceivingRules.Receive(request, "B", 2, ItemCondition.AsNew, now).Accepted);
            Assert.False(ReceivingRules.Receive(request, "B", 1, ItemCondition.NotInspected, now).Accepted);
            Assert.True(ReceivingRules.Receive(request, "B", 1, ItemCondition.Opened, now).Complete);
        }

        [Fact]
        public void Rejected_returns_cannot_be_received()
        {
            var request = Approved();
            request.Status = ReturnStatus.Rejected;
            Assert.False(ReceivingRules.Receive(request, "A", 1, ItemCondition.AsNew, DateTime.UtcNow).Accepted);
        }

        [Fact]
        public void Refund_batch_file_layout()
        {
            var request = new ReturnRequest
            {
                RmaNumber = "R2410-000001-0",
                OrderNumber = "SO-1",
                CustomerName = "Müller; Anna",
                CustomerEmail = "anna@example.com",
                Currency = "EUR",
                RefundAmount = 94.05m,
                LabelFee = 5.95m,
                RestockingFee = 0m,
                InspectedUtc = new DateTime(2024, 10, 2, 13, 0, 0),
                DecidedBy = "CORP\\jdoe"
            };
            request.Lines.Add(new ReturnLine { Quantity = 1, ReceivedQuantity = 1, UnitPrice = 100m });

            var writer = new StringWriter();
            RefundBatchFile.Write(writer, "20241003_0630", new[] { request });
            var lines = writer.ToString().Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(RefundBatchFile.Header, lines[0]);
            Assert.Equal("20241003_0630;R2410-000001-0;SO-1;Müller, Anna;anna@example.com;EUR;100.00;0.00;5.95;94.05;2024-10-02;CORP\\jdoe", lines[1]);
            Assert.Equal("REFUND_20241003_0630.csv", RefundBatchFile.FileName("20241003_0630"));
            Assert.Equal(28591, RefundBatchFile.FileEncoding.CodePage);
            Assert.Equal(2, lines.Length);
            Assert.True(lines.All(l => l.Split(';').Length == 12));
        }
    }
}
