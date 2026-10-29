using CsvHelper.Configuration.Attributes;

namespace ExpenseClaims.Jobs.Hr;

/// <summary>One row of the HR system's employee export (semicolon separated, UTF-8 with BOM).</summary>
public sealed class EmployeeRecord
{
    [Name("PersonnelNumber")] public string EmployeeNumber { get; set; } = "";
    [Name("UserPrincipalName")] public string Upn { get; set; } = "";
    [Name("FirstName")] public string FirstName { get; set; } = "";
    [Name("LastName")] public string LastName { get; set; } = "";
    [Name("Email")] public string Email { get; set; } = "";
    [Name("CostCenter")] public string CostCenter { get; set; } = "";
    [Name("SupervisorNumber")] public string? ManagerEmployeeNumber { get; set; }
    [Name("BankAccount")] public string? Iban { get; set; }
    [Name("BankBic")] public string? Bic { get; set; }
    [Name("EmploymentEnd")] [Format("dd.MM.yyyy")] public DateTime? EmploymentEnd { get; set; }
}
