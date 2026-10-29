namespace Lucerne.Lending.Domain;

public enum EmploymentType
{
    Permanent = 1,
    FixedTerm = 2,
    SelfEmployed = 3,
    Pensioner = 4,
    Student = 5,
    Unemployed = 6,
    Other = 9
}

public enum HousingType
{
    Owner = 1,
    Rent = 2,
    RightOfOccupancy = 3,
    WithParents = 4,
    Other = 9
}

public enum MaritalStatus
{
    Single = 1,
    Married = 2,
    Cohabiting = 3,
    Divorced = 4,
    Widowed = 5
}

public sealed class Applicant
{
    public long Id { get; set; }
    public bool IsMain { get; set; } = true;
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
    public MaritalStatus MaritalStatus { get; set; } = MaritalStatus.Single;
    public HousingType Housing { get; set; } = HousingType.Rent;
    public int Adults { get; set; } = 1;
    public int Children { get; set; }
    public EmploymentType Employment { get; set; } = EmploymentType.Permanent;
    public string Employer { get; set; } = "";
    public DateOnly? EmployedSince { get; set; }
    public bool OnProbation { get; set; }
    /// <summary>Gross monthly income (EUR) as declared.</summary>
    public decimal GrossMonthlyIncome { get; set; }
    /// <summary>Net monthly income (EUR) as declared.</summary>
    public decimal NetMonthlyIncome { get; set; }
    public decimal HousingCostMonthly { get; set; }
    public decimal OtherLoanPaymentsMonthly { get; set; }
    public decimal OtherLoanBalance { get; set; }
    public bool PoliticallyExposed { get; set; }
    /// <summary>Lucerne customer number (CIF) when known to the core.</summary>
    public string? CoreCustomerNumber { get; set; }

    public string FullName => $"{FirstName} {LastName}".Trim();
}
