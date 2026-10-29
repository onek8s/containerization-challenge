namespace CityPermits.Contracts;

/// <summary>Paystream notification body (API version 2021-04).</summary>
public sealed class PaystreamNotification
{
    public string EventId { get; set; } = "";
    public string EventType { get; set; } = "";
    public DateTime Created { get; set; }
    public PaystreamPayment Payment { get; set; } = new();
}

public sealed class PaystreamPayment
{
    public string PaymentId { get; set; } = "";
    public string MerchantReference { get; set; } = "";
    public long AmountMinor { get; set; }
    public string Currency { get; set; } = "GBP";
    public string Status { get; set; } = "";
    public string? CardBrand { get; set; }
    public string? Last4 { get; set; }
    public string? FailureCode { get; set; }
}
