namespace ExpenseClaims.Core.Claims;

/// <summary>Values of one claim line that the expense policy is checked against.</summary>
public sealed record ClaimLineInput(
    int LineNumber,
    DateOnly ExpenseDate,
    ExpenseCategory Category,
    decimal Amount,
    string Currency,
    decimal? Kilometres = null,
    PerDiemType PerDiem = PerDiemType.None,
    string? Country = null,
    int Attendees = 0,
    bool HasReceipt = false,
    string? Description = null);
