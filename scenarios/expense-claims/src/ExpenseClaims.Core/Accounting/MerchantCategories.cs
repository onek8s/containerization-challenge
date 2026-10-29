using ExpenseClaims.Core.Claims;

namespace ExpenseClaims.Core.Accounting;

/// <summary>Suggests an expense category for a card transaction from its merchant category code (ISO 18245).</summary>
public static class MerchantCategories
{
    public static ExpenseCategory Suggest(string? mcc)
    {
        if (!int.TryParse(mcc, out var code))
            return ExpenseCategory.Other;
        return code switch
        {
            >= 3000 and <= 3299 => ExpenseCategory.Travel,        // airlines
            >= 3351 and <= 3441 => ExpenseCategory.Travel,        // car rental
            >= 3501 and <= 3999 => ExpenseCategory.Accommodation, // hotels
            4111 or 4112 or 4121 or 4131 or 4411 or 4511 or 4582 or 4789 or 7512 or 7523 => ExpenseCategory.Travel,
            7011 => ExpenseCategory.Accommodation,
            5811 or 5812 or 5813 or 5814 => ExpenseCategory.Meals,
            8220 or 8241 or 8244 or 8249 or 8299 => ExpenseCategory.Training,
            5045 or 5732 or 5734 or 5946 => ExpenseCategory.Equipment,
            _ => ExpenseCategory.Other
        };
    }
}
