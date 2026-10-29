using System;
using System.Linq;

namespace ProductReturns.Core
{
    public class ScanResult
    {
        public bool Accepted { get; set; }
        public string Message { get; set; }
        public bool Complete { get; set; }
    }

    /// <summary>Applies warehouse scans of returned items to a return request.</summary>
    public static class ReceivingRules
    {
        public static bool CanReceive(ReturnStatus status) =>
            status == ReturnStatus.Approved || status == ReturnStatus.LabelIssued || status == ReturnStatus.Received;

        public static ScanResult Receive(ReturnRequest request, string sku, int quantity, ItemCondition condition, DateTime utcNow)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            if (!CanReceive(request.Status))
            {
                return new ScanResult { Message = "Return " + request.RmaNumber + " is " + request.Status + " and cannot be received." };
            }

            if (quantity <= 0)
            {
                return new ScanResult { Message = "Quantity must be at least 1." };
            }

            if (condition == ItemCondition.NotInspected)
            {
                return new ScanResult { Message = "Condition grade is required." };
            }

            var line = request.Lines.FirstOrDefault(l =>
                string.Equals(l.Sku, sku, StringComparison.OrdinalIgnoreCase) && l.ReceivedQuantity < l.Quantity);
            if (line == null)
            {
                return new ScanResult { Message = "SKU " + sku + " is not expected on " + request.RmaNumber + "." };
            }

            var open = line.Quantity - line.ReceivedQuantity;
            if (quantity > open)
            {
                return new ScanResult { Message = "Only " + open + " of " + sku + " expected." };
            }

            // A line received in several scans keeps the worst condition.
            line.Condition = line.ReceivedQuantity == 0 || condition > line.Condition ? condition : line.Condition;
            line.ReceivedQuantity += quantity;
            request.Status = ReturnStatus.Received;
            if (request.ReceivedUtc == null)
            {
                request.ReceivedUtc = utcNow;
            }

            var complete = request.Lines.All(l => l.ReceivedQuantity >= l.Quantity);
            return new ScanResult { Accepted = true, Complete = complete, Message = complete ? "All items received." : "Item received." };
        }
    }
}
