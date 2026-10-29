using VanArsdel.BuildingBlocks.Exceptions;

namespace VanArsdel.Returns.Api.Domain;

public static class ReturnStatus
{
    public const string Registered = "Registered";
    public const string Received = "Received";
    public const string Completed = "Completed";
    public const string Rejected = "Rejected";
}

public static class ReturnGrades
{
    /// <summary>As new, back to stock.</summary>
    public const string A = "A";

    /// <summary>Minor marks, back to stock (outlet).</summary>
    public const string B = "B";

    /// <summary>Damaged, written off; refunded anyway when the reason is "Faulty".</summary>
    public const string C = "C";

    public static readonly string[] All = { A, B, C };
}

public class ReturnRequest
{
    public Guid Id { get; set; }
    public string RmaNo { get; set; } = "";
    public string OrderNo { get; set; } = "";
    public Guid? OrderId { get; set; }
    public string Channel { get; set; } = "Web";
    public string StoreCode { get; set; }
    public string CustomerEmail { get; set; }
    public string LoyaltyCardNo { get; set; }
    public string Market { get; set; } = "FI";
    public string Currency { get; set; } = "EUR";
    public string Status { get; set; } = ReturnStatus.Registered;
    public string PaymentReference { get; set; }
    public string PaymentMethod { get; set; }
    public string RefundMethod { get; set; }
    public decimal RefundAmount { get; set; }
    public string RefundReference { get; set; }
    public string ReturnTrackingNo { get; set; }
    public string ReturnLabelUrl { get; set; }
    public string ReceivedAt { get; set; }
    public string RejectReason { get; set; }
    public string HandledBy { get; set; }
    public DateTime OrderPlacedUtc { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? ReceivedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
    public List<ReturnLine> Lines { get; set; } = new();

    public decimal LinesValue => Lines.Sum(l => l.Quantity * l.UnitPrice);

    public void Receive(string location, IEnumerable<(string Sku, int Quantity)> received, string by, DateTime now)
    {
        if (Status != ReturnStatus.Registered) throw new DomainException($"Return {RmaNo} is {Status}.");
        foreach (var (sku, qty) in received)
        {
            var line = Lines.SingleOrDefault(l => string.Equals(l.Sku, sku, StringComparison.OrdinalIgnoreCase))
                       ?? throw new DomainException($"{sku} is not on return {RmaNo}.");
            if (qty > line.Quantity) throw new DomainException($"{qty} x {sku} received, {line.Quantity} expected.");
            line.ReceivedQuantity = qty;
        }
        ReceivedAt = location;
        ReceivedUtc = now;
        HandledBy = by;
        Status = ReturnStatus.Received;
    }

    /// <summary>Grades the received items and works out the refund.</summary>
    public decimal Inspect(IDictionary<string, string> grades)
    {
        if (Status != ReturnStatus.Received) throw new DomainException($"Return {RmaNo} must be received first.");
        decimal refund = 0;
        foreach (var line in Lines.Where(l => l.ReceivedQuantity > 0))
        {
            if (!grades.TryGetValue(line.Sku, out var grade) || !ReturnGrades.All.Contains(grade))
            {
                throw new DomainException($"Grade (A, B or C) missing for {line.Sku}.");
            }
            line.Grade = grade;
            var refundable = grade != ReturnGrades.C || line.ReasonCode == "Faulty";
            if (refundable) refund += line.ReceivedQuantity * line.UnitPrice;
        }
        return Math.Round(refund, 2);
    }
}

public class ReturnLine
{
    public long Id { get; set; }
    public Guid ReturnId { get; set; }
    public string Sku { get; set; } = "";
    public int Quantity { get; set; }
    public int ReceivedQuantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string ReasonCode { get; set; } = "Other";
    public string Grade { get; set; }
}

public static class ReturnReasons
{
    public static readonly string[] All = { "TooSmall", "TooLarge", "NotAsDescribed", "Faulty", "ChangedMind", "WrongItem", "Other" };
}
