namespace ExpenseClaims.Data.Entities;

/// <summary>Employee master data, imported nightly from the HR system export.</summary>
public class Employee
{
    public int Id { get; set; }
    public string EmployeeNumber { get; set; } = "";

    /// <summary>User principal name, matched to the <c>upn</c> claim of the access token.</summary>
    public string Upn { get; set; } = "";

    public string DisplayName { get; set; } = "";
    public string Email { get; set; } = "";
    public string CostCenter { get; set; } = "";
    public string? ManagerEmployeeNumber { get; set; }
    public string? Iban { get; set; }
    public string? Bic { get; set; }
    public bool Active { get; set; } = true;
    public DateTime UpdatedAt { get; set; }
}
