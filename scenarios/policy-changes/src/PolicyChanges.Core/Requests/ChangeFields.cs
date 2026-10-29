namespace PolicyChanges.Core.Requests;

/// <summary>Field names used in change requests, the partner API and the PolicyDesk flows.</summary>
public static class ChangeFields
{
    public const string Street = "Street";
    public const string PostalCode = "PostalCode";
    public const string City = "City";
    public const string Country = "Country";

    public const string PaymentMethod = "PaymentMethod";
    public const string Iban = "Iban";
    public const string AccountHolder = "AccountHolder";
    public const string BillingDay = "BillingDay";

    /// <summary>One beneficiary per line: <c>name|yyyy-MM-dd|share</c>.</summary>
    public const string Beneficiaries = "Beneficiaries";

    public const string Reason = "Reason";
    public const string RefundIban = "RefundIban";

    public static IReadOnlyList<string> For(ChangeType type) => type switch
    {
        ChangeType.AddressChange => new[] { Street, PostalCode, City, Country },
        ChangeType.PaymentMethodChange => new[] { PaymentMethod, Iban, AccountHolder, BillingDay },
        ChangeType.BeneficiaryChange => new[] { Beneficiaries },
        ChangeType.Cancellation => new[] { Reason, RefundIban },
        _ => Array.Empty<string>(),
    };
}

public static class PaymentMethods
{
    public const string DirectDebit = "DirectDebit";
    public const string Invoice = "Invoice";

    public static readonly IReadOnlyList<string> All = new[] { DirectDebit, Invoice };
}

public static class CancellationReasons
{
    public const string CustomerRequest = "CustomerRequest";
    public const string VehicleSold = "VehicleSold";
    public const string Death = "Death";
    public const string Duplicate = "Duplicate";

    public static readonly IReadOnlyList<string> All = new[] { CustomerRequest, VehicleSold, Death, Duplicate };
}

public static class Countries
{
    public static readonly IReadOnlyList<string> All = new[] { "FI", "SE", "NO", "EE" };
}
