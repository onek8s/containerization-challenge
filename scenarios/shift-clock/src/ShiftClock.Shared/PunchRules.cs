namespace ShiftClock.Shared;

public enum PunchDecision
{
    Accepted,
    Duplicate,
    UnknownBadge
}

/// <summary>
/// Decides the direction of a new punch from the employee's previous punches.
/// </summary>
public sealed class PunchRules
{
    public TimeSpan DuplicateWindow { get; init; } = TimeSpan.FromSeconds(90);

    /// <summary>A shift open longer than this is treated as a missed clock-out.</summary>
    public TimeSpan MaxShiftLength { get; init; } = TimeSpan.FromHours(16);

    public PunchDecision Evaluate(Punch? lastPunch, DateTime now, out PunchDirection direction)
    {
        direction = PunchDirection.In;
        if (lastPunch is null)
            return PunchDecision.Accepted;

        if (now - lastPunch.Timestamp < DuplicateWindow)
        {
            direction = lastPunch.Direction;
            return PunchDecision.Duplicate;
        }

        if (lastPunch.Direction == PunchDirection.In && now - lastPunch.Timestamp <= MaxShiftLength)
            direction = PunchDirection.Out;

        return PunchDecision.Accepted;
    }

    /// <summary>Pairs In/Out punches and sums worked hours per employee for a period.</summary>
    public static IReadOnlyList<WorkedHours> Summarize(IEnumerable<Punch> punches)
    {
        var result = new List<WorkedHours>();
        foreach (var group in punches.GroupBy(p => p.EmployeeNumber).OrderBy(g => g.Key))
        {
            var ordered = group.OrderBy(p => p.Timestamp).ToList();
            var total = TimeSpan.Zero;
            var open = 0;
            DateTime? inAt = null;
            foreach (var p in ordered)
            {
                if (p.Direction == PunchDirection.In)
                {
                    if (inAt is not null)
                        open++;
                    inAt = p.Timestamp;
                }
                else if (inAt is not null)
                {
                    total += p.Timestamp - inAt.Value;
                    inAt = null;
                }
            }
            if (inAt is not null)
                open++;

            result.Add(new WorkedHours(group.Key, ordered[0].EmployeeName, Math.Round(total.TotalHours, 2), open));
        }
        return result;
    }
}

public sealed record WorkedHours(string EmployeeNumber, string EmployeeName, double Hours, int UnpairedPunches);
