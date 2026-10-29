using System.Globalization;
using System.Text;

namespace FurnaceRecords.Core;

public static class SampleCsv
{
    /// <summary>One row per timestamp, one column per channel, semicolon separated.</summary>
    public static string Write(IReadOnlyList<Sample> samples, IReadOnlyDictionary<int, string> channelNames, TimeZoneInfo zone)
    {
        var channels = channelNames.Keys.OrderBy(c => c).ToList();
        var sb = new StringBuilder();
        sb.Append("Time");
        foreach (var c in channels)
        {
            sb.Append(';').Append(Escape(channelNames[c]));
        }
        sb.AppendLine();

        foreach (var row in samples.GroupBy(s => s.AtUtc).OrderBy(g => g.Key))
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(row.Key, zone);
            sb.Append(local.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            var values = row.ToDictionary(s => s.Channel, s => s.Celsius);
            foreach (var c in channels)
            {
                sb.Append(';');
                if (values.TryGetValue(c, out var v))
                {
                    sb.Append(v.ToString("0.0", CultureInfo.InvariantCulture));
                }
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static string Escape(string s) =>
        s.Contains(';') || s.Contains('"') ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
}
