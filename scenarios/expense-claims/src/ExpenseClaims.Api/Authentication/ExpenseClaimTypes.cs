namespace ExpenseClaims.Api.Authentication;

/// <summary>Claim names as issued by ADFS with the application group's issuance transform rules.</summary>
public static class ExpenseClaimTypes
{
    public const string Upn = "upn";
    public const string Role = "role";
    public const string Name = "unique_name";
    public const string Scope = "scp";
}

public static class Roles
{
    public const string Finance = "Finance";
    public const string Administrator = "Administrator";
}

public static class Policies
{
    public const string Employee = "Employee";
    public const string Finance = "Finance";
    public const string Administrator = "Administrator";
}
