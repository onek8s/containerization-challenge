namespace BatchRelease.Core.Rules;

public static class ShelfLife
{
    /// <summary>
    /// Expiry is the last day of the month in which the shelf life ends, counted from the
    /// month of manufacture.
    /// </summary>
    public static DateOnly ExpiryDate(DateOnly manufacturedOn, int shelfLifeMonths)
    {
        var month = new DateOnly(manufacturedOn.Year, manufacturedOn.Month, 1).AddMonths(shelfLifeMonths);
        return month.AddDays(-1);
    }

    /// <summary>Whole months between <paramref name="today"/> and the expiry date.</summary>
    public static int RemainingMonths(DateOnly today, DateOnly expiresOn)
    {
        if (expiresOn <= today)
            return 0;

        var months = (expiresOn.Year - today.Year) * 12 + expiresOn.Month - today.Month;
        if (expiresOn.Day < today.Day && expiresOn.Day != DateTime.DaysInMonth(expiresOn.Year, expiresOn.Month))
            months--;
        return Math.Max(months, 0);
    }
}
