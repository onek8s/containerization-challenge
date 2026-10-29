namespace Proseware.ControlTower.Integration.Edifact;

/// <summary>Interchange control references: yyMMddHHmm + 4-digit counter, unique per sender per day.</summary>
public sealed class ControlReferences
{
    private int _counter;

    public string Next(DateTimeOffset now) =>
        $"{now.UtcDateTime:yyMMddHHmm}{Interlocked.Increment(ref _counter) % 10000:D4}";

    public static string FileName(string partnerId, string messageType, string controlReference) =>
        $"{messageType}_{partnerId}_{controlReference}.edi";
}
