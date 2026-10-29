namespace FreightRates.Core;

public sealed record FuelSurchargeStep(decimal DieselPriceFrom, decimal Percent);

public static class FuelSurcharge
{
    /// <summary>
    /// Returns the surcharge percent for the weekly national average diesel price
    /// using a step table ordered by price.
    /// </summary>
    public static decimal PercentFor(decimal dieselPrice, IEnumerable<FuelSurchargeStep> table)
    {
        var percent = 0m;
        foreach (var step in table.OrderBy(s => s.DieselPriceFrom))
        {
            if (dieselPrice >= step.DieselPriceFrom)
                percent = step.Percent;
            else
                break;
        }
        return percent;
    }

    /// <summary>The surcharge week starts on Monday; a shipment uses the price published for its week.</summary>
    public static DateOnly WeekOf(DateOnly date)
    {
        var offset = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-offset);
    }
}
