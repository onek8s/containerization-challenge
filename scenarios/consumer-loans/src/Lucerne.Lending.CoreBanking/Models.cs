namespace Lucerne.Lending.CoreBanking;

public sealed record CoreCustomer(
    string CustomerNumber,
    string PersonalIdentityCode,
    string Name,
    string Street,
    string PostalCode,
    string City,
    string Segment,
    string KycStatus,
    DateOnly CustomerSince,
    string InternalRating,
    bool Deceased,
    bool Blocked,
    string BranchCode)
{
    /// <summary>KYC status 'C' = complete, 'R' = review due, 'P' = pending, 'X' = refused.</summary>
    public bool KycComplete => KycStatus == "C";
}

public sealed record CoreAccount(string AccountNumber, string Iban, string ProductCode, string Type, decimal Balance, string Role);

public sealed record CoreLoanSummary(
    string AccountNumber,
    string ProductCode,
    decimal OriginalAmount,
    decimal Balance,
    decimal NominalRatePercent,
    DateOnly? NextDueDate,
    decimal NextInstalment,
    decimal Arrears,
    int DaysPastDue,
    string Status,
    string CustomerNumber = "");

public sealed record CoreExposure(
    string CustomerNumber,
    decimal TotalLimit,
    decimal TotalBalance,
    decimal TotalArrears,
    int MaxDaysPastDue,
    int ActiveLoans,
    decimal MonthlyInstalments,
    IReadOnlyList<CoreLoanSummary> Loans);

public sealed record NewCustomerRequest(
    string PersonalIdentityCode,
    string FirstName,
    string LastName,
    string Street,
    string PostalCode,
    string City,
    string Citizenship,
    string Email,
    string Phone,
    string BranchCode,
    string Officer);

public sealed record LoanBookingRequest(
    string ApplicationReference,
    string CustomerNumber,
    string? CoBorrowerNumber,
    string CoreProductCode,
    decimal Amount,
    int TermMonths,
    string ReferenceRateCode,
    decimal MarginPercent,
    decimal NominalRatePercent,
    decimal ArrangementFee,
    decimal MonthlyFee,
    int DueDay,
    DateOnly FirstDueDate,
    string BranchCode,
    string Officer);

public sealed record LoanBookingResult(bool Success, string? AccountNumber, string ReturnCode, string Message);

public sealed record DisbursementRequest(string AccountNumber, decimal Amount, string TargetIban, string ReferenceText, string Officer);

public sealed record DisbursementResult(bool Success, string? CoreTransactionId, bool ExternalTransfer, string ReturnCode, string Message);

public sealed record CorePayment(DateOnly BookingDate, string Type, decimal Amount, decimal Principal, decimal Interest,
    decimal Fees, decimal BalanceAfter);

public sealed record CoreLoanEvent(string EventType, string AccountNumber, DateTime Timestamp, decimal Amount,
    decimal Arrears, int DaysPastDue, string Raw)
{
    public const string Due = "DUE";
    public const string Paid = "PAID";
    public const string Missed = "MISS";
    public const string DaysPastDueChanged = "DPD";
    public const string Settled = "SETL";
    public const string Disbursed = "DISB";
}

public sealed record PlanInstalmentRequest(DateOnly DueDate, decimal Amount);

public sealed record CoreOperationResult(bool Success, string ReturnCode, string Message)
{
    public static CoreOperationResult Ok(string message = "") => new(true, "00", message);
}

public class CoreBankingException : Exception
{
    public CoreBankingException(string message, Exception? inner = null) : base(message, inner) { }
    public string? ReturnCode { get; init; }
}
