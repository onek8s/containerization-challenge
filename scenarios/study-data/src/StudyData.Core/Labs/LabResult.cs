using System.Globalization;

namespace StudyData.Core.Labs;

public enum LabFlag
{
    Normal,
    Low,
    High,
    CriticalLow,
    CriticalHigh,
    Unknown
}

public class LabResult
{
    public int Id { get; set; }
    public int StudyId { get; set; }
    public int SubjectId { get; set; }
    public string VisitCode { get; set; } = "";
    public string Accession { get; set; } = "";
    public DateTime CollectedAt { get; set; }
    public string TestCode { get; set; } = "";
    public string TestName { get; set; } = "";
    public string? Value { get; set; }
    public string? Unit { get; set; }
    public decimal? LowerLimit { get; set; }
    public decimal? UpperLimit { get; set; }
    public LabFlag Flag { get; set; }
    public int? Grade { get; set; }
    public string SourceFile { get; set; } = "";
    public DateTime ImportedAt { get; set; }
}

public class LabTransferFile
{
    public int Id { get; set; }
    public string FileName { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public DateTime ImportedAt { get; set; }
    public int Rows { get; set; }
    public int Rejected { get; set; }
    public string? Errors { get; set; }
}

/// <summary>Reference range flags and simplified CTCAE-style grades for the central lab panel.</summary>
public static class LabFlags
{
    private static readonly Dictionary<string, Func<decimal, decimal?, decimal?, int?>> Graders = new(StringComparer.OrdinalIgnoreCase)
    {
        // Neutrophils 10^9/L
        ["ANC"] = (v, low, _) => v >= (low ?? 1.5m) ? 0 : v >= 1.5m ? 1 : v >= 1.0m ? 2 : v >= 0.5m ? 3 : 4,
        // Platelets 10^9/L
        ["PLAT"] = (v, low, _) => v >= (low ?? 150m) ? 0 : v >= 75m ? 1 : v >= 50m ? 2 : v >= 25m ? 3 : 4,
        // Haemoglobin g/L
        ["HGB"] = (v, low, _) => v >= (low ?? 120m) ? 0 : v >= 100m ? 1 : v >= 80m ? 2 : 3,
        // Creatinine umol/L, by multiples of ULN
        ["CREAT"] = (v, _, high) => Uln(v, high ?? 90m, 1.5m, 3m, 6m),
        ["ALT"] = (v, _, high) => Uln(v, high ?? 45m, 3m, 5m, 20m),
        ["AST"] = (v, _, high) => Uln(v, high ?? 40m, 3m, 5m, 20m),
        ["BILI"] = (v, _, high) => Uln(v, high ?? 20m, 1.5m, 3m, 10m)
    };

    public static (LabFlag Flag, int? Grade) Evaluate(string testCode, string? value, decimal? low, decimal? high)
    {
        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var v))
        {
            return (LabFlag.Unknown, null);
        }

        int? grade = Graders.TryGetValue(testCode, out var g) ? g(v, low, high) : null;
        LabFlag flag;
        if (low is not null && v < low)
        {
            flag = grade >= 3 ? LabFlag.CriticalLow : LabFlag.Low;
        }
        else if (high is not null && v > high)
        {
            flag = grade >= 3 ? LabFlag.CriticalHigh : LabFlag.High;
        }
        else
        {
            flag = LabFlag.Normal;
        }

        return (flag, grade);
    }

    private static int Uln(decimal v, decimal uln, decimal g2, decimal g3, decimal g4)
    {
        if (v <= uln)
        {
            return 0;
        }

        var x = v / uln;
        return x <= g2 ? 1 : x <= g3 ? 2 : x <= g4 ? 3 : 4;
    }
}
