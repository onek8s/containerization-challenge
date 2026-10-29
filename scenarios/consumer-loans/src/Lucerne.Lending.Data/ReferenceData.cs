using Lucerne.Lending.Domain;

namespace Lucerne.Lending.Data;

/// <summary>Product catalogue as approved by the product committee (PC 2023/04).</summary>
public static class ReferenceData
{
    public static readonly LoanProduct[] Products =
    {
        new()
        {
            Code = "PL", Name = "Lucerne Personal Loan", CoreProductCode = "CL01", MinAmount = 2000, MaxAmount = 50000, MinTermMonths = 12,
            MaxTermMonths = 120, ReferenceRate = ReferenceRateType.Euribor12M, BaseMarginPercent = 3.90m, ArrangementFee = 90, MonthlyFee = 4.50m,
            Purposes = new[] { LoanPurpose.Other, LoanPurpose.Travel, LoanPurpose.Household, LoanPurpose.Studies, LoanPurpose.Renovation }
        },
        new()
        {
            Code = "CAR", Name = "Lucerne Car Loan", CoreProductCode = "CL02", MinAmount = 5000, MaxAmount = 70000, MinTermMonths = 12,
            MaxTermMonths = 84, ReferenceRate = ReferenceRateType.Euribor12M, BaseMarginPercent = 2.40m, ArrangementFee = 150, MonthlyFee = 0m,
            RequiresCollateral = true, Purposes = new[] { LoanPurpose.Car }
        },
        new()
        {
            Code = "RENO", Name = "Home Renovation Loan", CoreProductCode = "CL03", MinAmount = 3000, MaxAmount = 60000, MinTermMonths = 24,
            MaxTermMonths = 144, ReferenceRate = ReferenceRateType.Euribor12M, BaseMarginPercent = 2.90m, ArrangementFee = 100, MonthlyFee = 3.00m,
            Purposes = new[] { LoanPurpose.Renovation }
        },
        new()
        {
            Code = "CONS", Name = "Consolidation Loan", CoreProductCode = "CL04", MinAmount = 5000, MaxAmount = 60000, MinTermMonths = 24,
            MaxTermMonths = 144, ReferenceRate = ReferenceRateType.Fixed, BaseMarginPercent = 7.90m, ArrangementFee = 0, MonthlyFee = 5.00m,
            Purposes = new[] { LoanPurpose.Consolidation }
        },
        new()
        {
            Code = "FLEX", Name = "Flexi Loan (closed for sale)", CoreProductCode = "CL09", MinAmount = 500, MaxAmount = 5000, MinTermMonths = 6,
            MaxTermMonths = 36, ReferenceRate = ReferenceRateType.Fixed, BaseMarginPercent = 14.90m, ArrangementFee = 30, MonthlyFee = 8.00m,
            AllowsCoApplicant = false, Active = false
        }
    };

    public static async Task SeedAsync(ProductRepository products)
    {
        foreach (var p in Products)
            if (await products.GetAsync(p.Code) == null)
                await products.SaveAsync(p);
    }
}
