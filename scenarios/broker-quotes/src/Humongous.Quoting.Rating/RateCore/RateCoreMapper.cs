// VB6 Currency (VT_CY) is still the type RateCore uses for money.
#pragma warning disable CS0618

using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Humongous.Quoting.Domain;
using Humongous.Quoting.Rating.Interop;

namespace Humongous.Quoting.Rating.RateCore;

/// <summary>Moves a risk into a RateCore Risk object and reads the result back.</summary>
[SupportedOSPlatform("windows")]
internal static class RateCoreMapper
{
    public static void Load(_Risk risk, RatingRequest request)
    {
        var r = request.Risk;
        risk.SetValue("Purpose", request.Purpose switch
        {
            RatingPurpose.MidTermAdjustment => "MTA",
            RatingPurpose.Renewal => "REN",
            _ => "NB"
        });
        risk.SetValue("Postcode", Postcode.Normalise(r.Insured.Postcode));
        risk.SetValue("TradeCode", r.Insured.TradeCode);
        risk.SetValue("YearsTrading", r.Insured.YearsTrading);
        risk.SetValue("Turnover", new CurrencyWrapper(r.Insured.Turnover));
        risk.SetValue("Construction", r.Premises.Construction switch
        {
            ConstructionType.TimberFrame => "TF",
            ConstructionType.NonStandard => "NS",
            _ => "STD"
        });
        risk.SetValue("Roof", r.Premises.Roof switch
        {
            RoofType.FlatFelt => "FLAT",
            RoofType.Thatch => "THATCH",
            RoofType.Other => "OTHER",
            _ => "STD"
        });
        risk.SetValue("YearBuilt", r.Premises.YearBuilt);
        risk.SetValue("Occupied", r.Premises.Occupied);
        risk.SetValue("Listed", r.Premises.ListedBuilding);
        risk.SetValue("Flood10", r.Premises.FloodInLast10Years);
        risk.SetValue("Alarm", (int)r.Premises.Alarm);

        if (r.MaterialDamage is { } md)
        {
            Section(risk, "MD");
            risk.SetSectionValue("MD", "Buildings", new CurrencyWrapper(md.Buildings));
            risk.SetSectionValue("MD", "Contents", new CurrencyWrapper(md.Contents));
            risk.SetSectionValue("MD", "Stock", new CurrencyWrapper(md.Stock));
            risk.SetSectionValue("MD", "Computer", new CurrencyWrapper(md.ComputerEquipment));
            risk.SetSectionValue("MD", "Excess", new CurrencyWrapper(md.Excess));
        }

        if (r.BusinessInterruption is { } bi)
        {
            Section(risk, "BI");
            risk.SetSectionValue("BI", "GrossProfit", new CurrencyWrapper(bi.GrossProfit));
            risk.SetSectionValue("BI", "Indemnity", bi.IndemnityPeriodMonths);
        }

        if (r.EmployersLiability is { } el)
        {
            Section(risk, "EL");
            risk.SetSectionValue("EL", "Employees", el.Employees);
            risk.SetSectionValue("EL", "Wageroll", new CurrencyWrapper(el.Wageroll));
            risk.SetSectionValue("EL", "ManualWageroll", new CurrencyWrapper(el.ManualWageroll));
        }

        if (r.PublicLiability is { } pl)
        {
            Section(risk, "PL");
            risk.SetSectionValue("PL", "Limit", new CurrencyWrapper(pl.LimitOfIndemnity));
            risk.SetSectionValue("PL", "Products", pl.ProductsSupplied);
            risk.SetSectionValue("PL", "WorkAway", pl.WorkAwayFromPremises);
        }

        foreach (var claim in r.Claims)
            risk.AddClaim(claim.Date.ToDateTime(TimeOnly.MinValue), claim.Section.ToCode(), claim.Amount);
    }

    public static RatingResult Read(_Risk risk, string engineVersion)
    {
        var sections = new List<SectionRating>();
        var count = risk.get_SectionCount();
        for (var i = 1; i <= count; i++) // VB6 collections are 1-based
        {
            sections.Add(new SectionRating(
                SectionCodes.Parse(risk.get_SectionCode(i)),
                risk.get_SectionPremium(i),
                (decimal)risk.get_SectionRate(i),
                risk.get_SectionBasis(i)));
        }

        var referrals = new List<RatingMessage>();
        var declines = new List<RatingMessage>();
        var messages = risk.get_MessageCount();
        for (var i = 1; i <= messages; i++)
        {
            var message = new RatingMessage(risk.get_MessageCode(i), risk.get_MessageText(i));
            if (risk.get_MessageType(i) == "D") declines.Add(message);
            else referrals.Add(message);
        }

        var clauses = risk.get_Clauses() is object[] array
            ? array.Select(c => Convert.ToString(c) ?? "").Where(c => c.Length > 0).ToList()
            : [];

        return new RatingResult
        {
            EngineVersion = engineVersion,
            TableVersion = risk.get_TableVersion(),
            Sections = sections,
            Referrals = referrals,
            Declines = declines,
            Clauses = clauses
        };
    }

    private static void Section(_Risk risk, string code)
    {
        if (!risk.AddSection(code))
            throw new RatingException($"RateCore rejected section {code}: {risk.get_ErrorText()}");
    }
}
