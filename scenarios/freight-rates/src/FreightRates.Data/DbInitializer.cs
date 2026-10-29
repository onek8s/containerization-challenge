using FreightRates.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FreightRates.Data;

public static class DbInitializer
{
    /// <summary>Development API key of the sample customer ACME01.</summary>
    public const string SampleApiKey = "fr_dev_acme01_0b7c4a2e";

    public static async Task InitializeAsync(FreightRatesContext db, bool seedSampleData, CancellationToken cancellationToken = default)
    {
        await db.Database.EnsureCreatedAsync(cancellationToken);

        if (!await db.Accessorials.AnyAsync(cancellationToken))
        {
            db.Accessorials.AddRange(
                new Accessorial { Code = "LGPU", Description = "Liftgate pickup", Amount = 75m },
                new Accessorial { Code = "LGDL", Description = "Liftgate delivery", Amount = 75m },
                new Accessorial { Code = "RESDL", Description = "Residential delivery", Amount = 95m },
                new Accessorial { Code = "INDL", Description = "Inside delivery", Amount = 85m, PerCwt = 6.50m },
                new Accessorial { Code = "APPT", Description = "Delivery appointment", Amount = 25m },
                new Accessorial { Code = "LTDAC", Description = "Limited access delivery", Amount = 90m },
                new Accessorial { Code = "HAZ", Description = "Hazardous materials", Amount = 45m },
                new Accessorial { Code = "NOTIFY", Description = "Notify before delivery", Amount = 15m });
        }

        if (!await db.FuelSurchargeSteps.AnyAsync(cancellationToken))
        {
            // 0.5 % per 5 cents above 2.50 USD, starting at 20 % (published table FSC-LTL-2024).
            for (var i = 0; i <= 60; i++)
                db.FuelSurchargeSteps.Add(new FuelSurchargeStep { DieselPriceFrom = 2.50m + i * 0.05m, Percent = 20m + i * 0.5m });
        }

        if (seedSampleData && !await db.Customers.AnyAsync(cancellationToken))
        {
            db.Customers.AddRange(
                new Customer { Code = "ACME01", Name = "Acme Industrial Supply", DiscountPercent = 72m, MinimumCharge = 110m, ApiKeyHash = ApiKeys.Hash(SampleApiKey) },
                new Customer { Code = "NWIND", Name = "Northwind Traders", DiscountPercent = 65m, MinimumCharge = 0m, ApiKeyHash = ApiKeys.Hash("fr_dev_nwind_51e09d3c") },
                new Customer { Code = "TAILSP", Name = "Tailspin Toys", DiscountPercent = 58.5m, MinimumCharge = 125m, FuelSurchargeOverridePercent = 24m, ApiKeyHash = ApiKeys.Hash("fr_dev_tailsp_9f2a77b1") });

            var monday = Core.FuelSurcharge.WeekOf(DateOnly.FromDateTime(DateTime.Today));
            var prices = new[] { 3.512m, 3.548m, 3.604m, 3.587m, 3.633m, 3.691m, 3.712m, 3.676m };
            for (var i = 0; i < prices.Length; i++)
                db.FuelPrices.Add(new FuelPrice { WeekStarting = monday.AddDays(-7 * (prices.Length - 1 - i)), DieselPrice = prices[i] });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
