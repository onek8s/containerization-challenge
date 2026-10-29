namespace Proseware.ControlTower.Domain;

/// <summary>
/// Terminal and customer times are kept with the time zone id of the location
/// (ids as configured in the location master data, e.g. "FLE Standard Time").
/// </summary>
public static class LocalTime
{
    private static readonly Dictionary<string, TimeZoneInfo> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static TimeZoneInfo Zone(string timeZoneId)
    {
        lock (Cache)
        {
            if (!Cache.TryGetValue(timeZoneId, out var zone))
            {
                zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                Cache[timeZoneId] = zone;
            }
            return zone;
        }
    }

    public static DateTimeOffset ToLocal(DateTimeOffset utc, string timeZoneId) =>
        TimeZoneInfo.ConvertTime(utc, Zone(timeZoneId));

    public static DateTimeOffset FromLocal(DateTime local, string timeZoneId)
    {
        var zone = Zone(timeZoneId);
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        return new DateTimeOffset(unspecified, zone.GetUtcOffset(unspecified));
    }

    /// <summary>Working day check used for pickup and delivery promises (Mon-Fri, local time).</summary>
    public static bool IsWorkingDay(DateTimeOffset utc, string timeZoneId)
    {
        var local = ToLocal(utc, timeZoneId);
        return local.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);
    }

    public static DateTimeOffset AddWorkingDays(DateTimeOffset utc, int days, string timeZoneId)
    {
        var result = utc;
        var added = 0;
        while (added < days)
        {
            result = result.AddDays(1);
            if (IsWorkingDay(result, timeZoneId)) added++;
        }
        return result;
    }
}
