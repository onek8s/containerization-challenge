namespace Lucerne.Lending.Contracts;

public sealed class ApplicantDto
{
    public string PersonalIdentityCode { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string StreetAddress { get; set; } = "";
    public string PostalCode { get; set; } = "";
    public string City { get; set; } = "";
    public string Citizenship { get; set; } = "FI";
    public bool ResidentInFinland { get; set; } = true;
    public string MaritalStatus { get; set; } = "Single";
    public string Housing { get; set; } = "Rent";
    public int Adults { get; set; } = 1;
    public int Children { get; set; }
    public string Employment { get; set; } = "Permanent";
    public string Employer { get; set; } = "";
    public DateTime? EmployedSince { get; set; }
    public bool OnProbation { get; set; }
    public decimal GrossMonthlyIncome { get; set; }
    public decimal NetMonthlyIncome { get; set; }
    public decimal HousingCostMonthly { get; set; }
    public decimal OtherLoanPaymentsMonthly { get; set; }
    public decimal OtherLoanBalance { get; set; }
    public bool PoliticallyExposed { get; set; }
}

public sealed class ApplicationRequest
{
    public string ProductCode { get; set; } = "";
    public string Purpose { get; set; } = "Other";
    public decimal Amount { get; set; }
    public int TermMonths { get; set; }
    public int DueDay { get; set; } = 15;
    public string Channel { get; set; } = "Web";
    public string? DisbursementIban { get; set; }
    public string? CollateralDescription { get; set; }
    public decimal? CollateralValue { get; set; }
    public bool ConsentBureau { get; set; }
    public bool ConsentMarketing { get; set; }
    public ApplicantDto Applicant { get; set; } = new();
    public ApplicantDto? CoApplicant { get; set; }
}

public sealed class ApplicationCreated
{
    public string Reference { get; set; } = "";
    public string Status { get; set; } = "";
}

public sealed class ApplicationSummaryDto
{
    public string Reference { get; set; } = "";
    public string ProductCode { get; set; } = "";
    public string ProductName { get; set; } = "";
    public decimal Amount { get; set; }
    public int TermMonths { get; set; }
    public string Status { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

public sealed class OfferDto
{
    public long Id { get; set; }
    public decimal Amount { get; set; }
    public int TermMonths { get; set; }
    public decimal NominalRatePercent { get; set; }
    public decimal ReferenceRatePercent { get; set; }
    public decimal MarginPercent { get; set; }
    public decimal AprPercent { get; set; }
    public decimal MonthlyPayment { get; set; }
    public decimal MonthlyFee { get; set; }
    public decimal ArrangementFee { get; set; }
    public decimal TotalRepayable { get; set; }
    public DateTime ValidUntilUtc { get; set; }
    public string Status { get; set; } = "";
}

public sealed class DocumentDto
{
    public long Id { get; set; }
    public string Kind { get; set; } = "";
    public string FileName { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
}

public sealed class ApplicationDetailDto
{
    public string Reference { get; set; } = "";
    public string ProductCode { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string Purpose { get; set; } = "";
    public decimal Amount { get; set; }
    public int TermMonths { get; set; }
    public int DueDay { get; set; }
    public string Status { get; set; } = "";
    public string StatusText { get; set; } = "";
    public string? DisbursementIban { get; set; }
    public string ApplicantName { get; set; } = "";
    public string? CoApplicantName { get; set; }
    public OfferDto? Offer { get; set; }
    public string? SigningUrl { get; set; }
    public string? CoreAccountNumber { get; set; }
    public bool CanCancel { get; set; }
    public List<DocumentDto> Documents { get; set; } = new();
    public DateTime CreatedUtc { get; set; }
}

public sealed class AcceptOfferRequest
{
    public long OfferId { get; set; }
    public string DisbursementIban { get; set; } = "";
    public bool ConfirmsInformationRead { get; set; }
}

public sealed class CustomerLoanDto
{
    public string AccountNumber { get; set; } = "";
    public string ProductName { get; set; } = "";
    public decimal OriginalAmount { get; set; }
    public decimal Balance { get; set; }
    public decimal NominalRatePercent { get; set; }
    public DateTime? NextDueDate { get; set; }
    public decimal NextInstalment { get; set; }
    public decimal Arrears { get; set; }
    public int DaysPastDue { get; set; }
    public string PaymentReference { get; set; } = "";
    public string Status { get; set; } = "";
}

public sealed class PaymentHistoryDto
{
    public DateTime BookingDate { get; set; }
    public string Type { get; set; } = "";
    public decimal Amount { get; set; }
    public decimal Principal { get; set; }
    public decimal Interest { get; set; }
    public decimal Fees { get; set; }
    public decimal BalanceAfter { get; set; }
}
