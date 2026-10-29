using StudyData.Core.Design;
using StudyData.Core.Subjects;

namespace StudyData.Core.Scheduling;

public record PlannedVisit(VisitDefinition Definition, DateOnly? Target, DateOnly? Earliest, DateOnly? Latest);

public static class VisitSchedule
{
    /// <summary>Target dates of the scheduled visits once the anchor (day 1) visit has a date.</summary>
    public static IReadOnlyList<PlannedVisit> Plan(Study study, DateOnly? anchorDate)
    {
        var anchor = study.Visits.SingleOrDefault(v => v.IsAnchor)
            ?? throw new InvalidOperationException($"Study {study.Code} has no anchor visit.");

        return study.Visits
            .Where(v => !v.Unscheduled)
            .OrderBy(v => v.Order)
            .Select(v =>
            {
                if (anchorDate is null)
                {
                    return new PlannedVisit(v, null, null, null);
                }

                // Study day 1 is the anchor date; there is no day 0, so negative days count back from day 1 without a gap.
                var offset = v.PlannedDay > 0 ? v.PlannedDay - anchor.PlannedDay : v.PlannedDay - anchor.PlannedDay + 1;
                var target = anchorDate.Value.AddDays(offset);
                return new PlannedVisit(v, target, target.AddDays(-v.WindowBefore), target.AddDays(v.WindowAfter));
            })
            .ToList();
    }

    public static int StudyDay(DateOnly anchorDate, DateOnly date)
    {
        var diff = date.DayNumber - anchorDate.DayNumber;
        return diff >= 0 ? diff + 1 : diff;
    }

    public static bool InWindow(PlannedVisit planned, DateOnly date) =>
        planned.Earliest is null || (date >= planned.Earliest && date <= planned.Latest);

    public static DateOnly? AnchorDate(Subject subject) =>
        subject.Visits.FirstOrDefault(v => v.Definition?.IsAnchor == true)?.Date;
}

public static class SubjectNumbers
{
    public static string Format(string pattern, string siteNumber, int sequence)
    {
        var result = pattern.Replace("{site}", siteNumber, StringComparison.Ordinal);
        var start = result.IndexOf("{seq", StringComparison.Ordinal);
        if (start < 0)
        {
            return result;
        }

        var end = result.IndexOf('}', start);
        var spec = result[(start + 4)..end].TrimStart(':');
        var seq = string.IsNullOrEmpty(spec) ? sequence.ToString() : sequence.ToString(spec);
        return result[..start] + seq + result[(end + 1)..];
    }
}

/// <summary>
/// Permuted block randomisation per site and stratum. The list is generated from the study seed so the
/// sequence can be reproduced by the unblinded statistician.
/// </summary>
public static class BlockRandomiser
{
    public static IReadOnlyList<string> Generate(IReadOnlyList<Arm> arms, int blocks, int seed)
    {
        if (arms.Count == 0)
        {
            throw new ArgumentException("No arms.", nameof(arms));
        }

        var block = arms.SelectMany(a => Enumerable.Repeat(a.Code, Math.Max(1, a.Ratio))).ToArray();
        var random = new Random(seed);
        var list = new List<string>(blocks * block.Length);
        for (var b = 0; b < blocks; b++)
        {
            var copy = (string[])block.Clone();
            for (var i = copy.Length - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                (copy[i], copy[j]) = (copy[j], copy[i]);
            }

            list.AddRange(copy);
        }

        return list;
    }

    public static int Seed(string studyCode, string siteNumber, string stratum)
    {
        // Stable across processes (string.GetHashCode is randomised per process).
        unchecked
        {
            var h = 17;
            foreach (var c in $"{studyCode}|{siteNumber}|{stratum}")
            {
                h = (h * 31) + c;
            }

            return h & 0x7fffffff;
        }
    }
}
