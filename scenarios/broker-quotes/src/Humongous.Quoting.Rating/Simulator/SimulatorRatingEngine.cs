using System.Diagnostics;
using Humongous.Quoting.Domain;

namespace Humongous.Quoting.Rating.Simulator;

/// <summary>
/// Stand-in for RateCore on development machines and in the training environment. Uses flat
/// indicative rates (table version SIMULATOR) and none of the product rate tables, zones or trade
/// loadings. Premiums from the simulator are not product premiums.
/// </summary>
public sealed class SimulatorRatingEngine : IRatingEngine, IRatingEngineStatus
{
    private long _calls;

    public string Name => "Simulator";

    public async Task<RatingResult> RateAsync(RatingRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        // RateCore takes 200-800 ms per risk on the rating servers; keep the UI honest.
        await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
        Interlocked.Increment(ref _calls);

        var risk = request.Risk;
        var sections = new List<SectionRating>();
        if (risk.MaterialDamage is { } md)
            sections.Add(Flat(SectionCode.MaterialDamage, md.TotalSumInsured, 0.0018m, "Total sum insured"));
        if (risk.BusinessInterruption is { } bi)
            sections.Add(Flat(SectionCode.BusinessInterruption, bi.GrossProfit * bi.IndemnityPeriodMonths / 12m, 0.0011m, "Gross profit x indemnity"));
        if (risk.EmployersLiability is { } el)
            sections.Add(Flat(SectionCode.EmployersLiability, el.Wageroll + el.ManualWageroll, 0.0045m, "Wageroll"));
        if (risk.PublicLiability is { } pl)
            sections.Add(Flat(SectionCode.PublicLiability, risk.Insured.Turnover, pl.LimitOfIndemnity >= 5_000_000m ? 0.0016m : 0.0012m, "Turnover"));

        var referrals = new List<RatingMessage>();
        if (Postcode.Area(risk.Insured.Postcode) is "HU" or "YO")
            referrals.Add(new RatingMessage("Z90", "Simulator: postcode area referred for flood."));

        var clauses = new List<string> { "GEN01" };
        if (risk.MaterialDamage is not null) clauses.Add("MD01");
        if (risk.EmployersLiability is not null) clauses.Add("EL01");
        if (risk.PublicLiability is not null) clauses.Add("PL01");

        return new RatingResult
        {
            EngineVersion = "Simulator 1.0",
            TableVersion = "SIMULATOR",
            Sections = sections,
            Referrals = referrals,
            Clauses = clauses,
            Duration = stopwatch.Elapsed
        };
    }

    public RatingEngineState GetState() => new(Name, "Simulator 1.0", 1, 0, 0, Interlocked.Read(ref _calls), null);

    private static SectionRating Flat(SectionCode section, decimal basis, decimal rate, string description) =>
        new(section, Money.Round(basis * rate), rate * 100m, description);
}
