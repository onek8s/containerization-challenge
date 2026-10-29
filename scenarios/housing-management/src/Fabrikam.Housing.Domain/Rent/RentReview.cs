using System;

namespace Fabrikam.Housing.Rent
{
    /// <summary>
    /// Annual rent review under the Rent Standard: social rents may rise by at most CPI (September) + 1%,
    /// and never above the formula rent cap for the number of bedrooms. Service charges follow actual cost.
    /// </summary>
    public class RentReview
    {
        public decimal CpiPercent { get; set; }
        public decimal ExtraPercent { get; set; } = 1.0m;

        public static readonly decimal[] FormulaRentCaps = { 0m, 155.73m, 163.52m, 171.30m, 179.09m, 186.87m, 194.66m };

        public decimal NewWeeklyNetRent(decimal currentRent, int bedrooms)
        {
            var increased = currentRent * (1 + (CpiPercent + ExtraPercent) / 100m);
            var cap = FormulaRentCaps[Math.Min(Math.Max(bedrooms, 1), FormulaRentCaps.Length - 1)];
            return Math.Round(Math.Min(increased, Math.Max(cap, currentRent)), 2, MidpointRounding.AwayFromZero);
        }
    }
}
