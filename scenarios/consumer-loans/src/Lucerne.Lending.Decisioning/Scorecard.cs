using System.Globalization;
using Lucerne.Lending.Domain;
using Newtonsoft.Json;

namespace Lucerne.Lending.Decisioning;

public sealed record ScoreLine(string Characteristic, string Value, int Points);

public sealed class ScorecardDefinition
{
    public string Version { get; set; } = "";
    public int CutOff { get; set; }
    public List<GradeBand> Grades { get; set; } = new();
    public List<Characteristic> Characteristics { get; set; } = new();

    public sealed class GradeBand
    {
        public string Grade { get; set; } = "";
        public int MinScore { get; set; }
        public decimal MaxAmount { get; set; }
    }

    public sealed class Characteristic
    {
        public string Name { get; set; } = "";
        public string Fact { get; set; } = "";
        public List<Bin>? Bins { get; set; }
        public Dictionary<string, int>? Values { get; set; }
    }

    public sealed class Bin
    {
        public decimal Max { get; set; }
        public int Points { get; set; }
    }
}

public sealed class Scorecard
{
    private readonly ScorecardDefinition _definition;

    public Scorecard(ScorecardDefinition definition) => _definition = definition;

    public static Scorecard LoadEmbedded()
    {
        using var stream = typeof(Scorecard).Assembly.GetManifestResourceStream("Lucerne.Lending.Decisioning.Rules.scorecard.json")!;
        using var reader = new StreamReader(stream);
        return new Scorecard(JsonConvert.DeserializeObject<ScorecardDefinition>(reader.ReadToEnd())!);
    }

    public string Version => _definition.Version;
    public int CutOff => _definition.CutOff;

    public (int Score, IReadOnlyList<ScoreLine> Lines) Score(CreditFacts facts)
    {
        var lines = new List<ScoreLine>();
        foreach (var c in _definition.Characteristics)
        {
            var (display, points) = Evaluate(c, facts);
            lines.Add(new ScoreLine(c.Name, display, points));
        }
        return (lines.Sum(l => l.Points), lines);
    }

    public RiskGrade Grade(int score) =>
        _definition.Grades.OrderByDescending(g => g.MinScore).FirstOrDefault(g => score >= g.MinScore) is { } band
            ? Enum.Parse<RiskGrade>(band.Grade)
            : RiskGrade.X;

    public decimal MaxAmount(RiskGrade grade) =>
        _definition.Grades.FirstOrDefault(g => g.Grade == grade.ToString())?.MaxAmount ?? 0m;

    private static (string Display, int Points) Evaluate(ScorecardDefinition.Characteristic c, CreditFacts facts)
    {
        object value = c.Fact switch
        {
            "InternalRatingClass" => facts.InternalRating.Length > 0 ? facts.InternalRating[..1] : "",
            _ => typeof(CreditFacts).GetProperty(c.Fact)?.GetValue(facts) ?? throw new InvalidOperationException($"Unknown fact {c.Fact}")
        };
        if (c.Values != null)
        {
            var key = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
            return (key, c.Values.TryGetValue(key, out var p) ? p : 0);
        }
        var number = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
        var bin = c.Bins!.FirstOrDefault(b => number <= b.Max) ?? c.Bins!.Last();
        return (number.ToString(CultureInfo.InvariantCulture), bin.Points);
    }
}
