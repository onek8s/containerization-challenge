using ExpenseClaims.Data;
using ExpenseClaims.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExpenseClaims.Api.Development;

/// <summary>Test employees and exchange rates for an empty development database.</summary>
public static class DevelopmentSeed
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ExpenseClaimsDbContext>();
        if (await db.Employees.AnyAsync())
            return;

        var now = DateTime.Now;
        Employee E(string number, string upn, string name, string cc, string? manager, string? iban) => new()
        {
            EmployeeNumber = number,
            Upn = upn,
            DisplayName = name,
            Email = upn,
            CostCenter = cc,
            ManagerEmployeeNumber = manager,
            Iban = iban,
            Bic = iban is null ? null : "NDEAFIHH",
            UpdatedAt = now
        };

        db.Employees.AddRange(
            E("100001", "helena.lindqvist@lamnahealthcare.com", "Helena Lindqvist", "1000", null, "FI4950009420028730"),
            E("100014", "mikko.salonen@lamnahealthcare.com", "Mikko Salonen", "1100", "100001", "FI2112345600000785"),
            E("100027", "laura.niemi@lamnahealthcare.com", "Laura Niemi", "1100", "100014", "FI5542345670000081"),
            E("100102", "sanna.koskinen@lamnahealthcare.com", "Sanna Koskinen", "2100", "100001", "FI1410093000123458"),
            E("100150", "jari.makinen@lamnahealthcare.com", "Jari Mäkinen", "2140", "100102", "FI3715903000000776"),
            E("100233", "aino.laine@lamnahealthcare.com", "Aino Laine", "2140", "100150", "FI1447380010009051"),
            E("100241", "oskari.heikkinen@lamnahealthcare.com", "Oskari Heikkinen", "2140", "100150", null));

        await db.SaveChangesAsync();
        var aino = await db.Employees.SingleAsync(e => e.EmployeeNumber == "100233");
        var jari = await db.Employees.SingleAsync(e => e.EmployeeNumber == "100150");
        db.CompanyCards.AddRange(
            new CompanyCard { MaskedNumber = "552235******4410", Employee = aino },
            new CompanyCard { MaskedNumber = "552235******7021", Employee = jari });

        var rates = new Dictionary<string, decimal> { ["USD"] = 1.0725m, ["SEK"] = 11.4520m, ["GBP"] = 0.8571m, ["NOK"] = 11.7385m, ["DKK"] = 7.4381m };
        for (var day = DateTime.Today.AddDays(-100); day <= DateTime.Today; day = day.AddDays(1))
        {
            if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                continue;
            foreach (var (currency, rate) in rates)
                db.ExchangeRates.Add(new ExchangeRate { Date = DateOnly.FromDateTime(day), Currency = currency, UnitsPerEuro = rate });
        }

        await db.SaveChangesAsync();
    }
}
