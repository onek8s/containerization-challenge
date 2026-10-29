namespace Lucerne.Lending.Decisioning;

/// <summary>
/// Flat set of facts the credit policy rules are evaluated against (rule expressions refer to these as input1.*).
/// Built by the application workflow from the application, the bureau report and the core customer data.
/// </summary>
public sealed class CreditFacts
{
    public string ProductCode { get; set; } = "";
    public decimal Amount { get; set; }
    public int TermMonths { get; set; }
    public bool RequiresCollateral { get; set; }
    public decimal CollateralValue { get; set; }
    public decimal LoanToValue { get; set; }
    public bool HasCoApplicant { get; set; }

    public int Age { get; set; }
    public int AgeAtMaturity { get; set; }
    public bool Resident { get; set; }
    public string Employment { get; set; } = "";
    public int MonthsEmployed { get; set; }
    public bool OnProbation { get; set; }
    public string Housing { get; set; } = "";
    public bool PoliticallyExposed { get; set; }
    public decimal NetIncome { get; set; }
    public decimal GrossIncome { get; set; }

    public bool BureauPersonFound { get; set; } = true;
    public int BureauScore { get; set; }
    public bool PaymentDefault { get; set; }
    public int PaymentDefaultCount { get; set; }
    public bool CreditBlock { get; set; }
    public bool DebtRestructuring { get; set; }
    public int CreditInquiries12M { get; set; }

    public bool ExistingCustomer { get; set; }
    public int CustomerMonths { get; set; }
    public string KycStatus { get; set; } = "";
    public bool CoreBlocked { get; set; }
    public bool Deceased { get; set; }
    public string InternalRating { get; set; } = "";
    public decimal ExistingBalance { get; set; }
    public decimal ExistingArrears { get; set; }
    public int ExistingMaxDpd { get; set; }

    public decimal Dsti { get; set; }
    public decimal Surplus { get; set; }
}
