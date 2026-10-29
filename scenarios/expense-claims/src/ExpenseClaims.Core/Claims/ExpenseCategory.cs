namespace ExpenseClaims.Core.Claims;

public enum ExpenseCategory
{
    Travel = 1,
    Accommodation = 2,
    Meals = 3,
    Mileage = 4,
    PerDiem = 5,
    Training = 6,
    Equipment = 7,
    Representation = 8,
    Other = 99
}

public enum PerDiemType
{
    None = 0,
    Partial = 1,
    Full = 2,
    Abroad = 3
}
