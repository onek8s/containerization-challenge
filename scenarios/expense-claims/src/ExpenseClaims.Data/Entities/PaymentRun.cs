namespace ExpenseClaims.Data.Entities;

public enum PaymentRunStatus
{
    Created = 0,
    Sent = 1,
    Accepted = 2,
    Failed = 3
}

public class PaymentRun
{
    public int Id { get; set; }
    public string MessageId { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateOnly ExecutionDate { get; set; }
    public int PaymentCount { get; set; }
    public decimal Total { get; set; }
    public string FileName { get; set; } = "";
    public PaymentRunStatus Status { get; set; }
    public string? BankReference { get; set; }
    public string? Error { get; set; }
    public DateTime? SentAt { get; set; }
}
