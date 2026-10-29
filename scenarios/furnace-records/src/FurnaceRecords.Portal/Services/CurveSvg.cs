using System.Globalization;
using System.Text;
using FurnaceRecords.Core;

namespace FurnaceRecords.Portal.Services;

/// <summary>Temperature curve of a load as inline SVG.</summary>
public static class CurveSvg
{
    private static readonly string[] Colors = { "#c0392b", "#7f8c8d", "#8e44ad", "#2c3e50", "#2980b9", "#16a085", "#d35400", "#27ae60", "#f39c12", "#34495e" };

    public static string Render(IReadOnlyList<Sample> samples, IReadOnlyDictionary<int, string> names, RecipeSpec recipe,
        DateTime? soakStart, DateTime? soakEnd, TimeZoneInfo zone, int width = 960, int height = 360)
    {
        if (samples.Count == 0)
        {
            return "<p class=\"muted\">No samples recorded.</p>";
        }

        const int left = 50, right = 10, top = 10, bottom = 30;
        var t0 = samples[0].AtUtc;
        var t1 = samples[^1].AtUtc;
        var span = Math.Max(60, (t1 - t0).TotalSeconds);
        var maxT = Math.Max(recipe.UpperLimit + 20, samples.Max(s => s.Celsius) + 10);
        const double minT = 0;
        double X(DateTime t) => left + (t - t0).TotalSeconds / span * (width - left - right);
        double Y(double c) => top + (maxT - c) / (maxT - minT) * (height - top - bottom);
        string F(double v) => v.ToString("0.#", CultureInfo.InvariantCulture);

        var sb = new StringBuilder();
        sb.Append($"<svg class=\"curve\" viewBox=\"0 0 {width} {height}\" role=\"img\" aria-label=\"Temperature curve\">");

        if (soakStart is not null && soakEnd is not null)
        {
            sb.Append($"<rect x=\"{F(X(soakStart.Value))}\" y=\"{top}\" width=\"{F(X(soakEnd.Value) - X(soakStart.Value))}\" height=\"{height - top - bottom}\" fill=\"#eaf5ea\" />");
        }
        foreach (var limit in new[] { recipe.LowerLimit, recipe.UpperLimit })
        {
            sb.Append($"<line x1=\"{left}\" x2=\"{width - right}\" y1=\"{F(Y(limit))}\" y2=\"{F(Y(limit))}\" stroke=\"#27ae60\" stroke-dasharray=\"4 3\" />");
        }
        for (var c = 0.0; c <= maxT; c += 100)
        {
            sb.Append($"<line x1=\"{left - 4}\" x2=\"{left}\" y1=\"{F(Y(c))}\" y2=\"{F(Y(c))}\" stroke=\"#555\" />");
            sb.Append($"<text x=\"{left - 6}\" y=\"{F(Y(c) + 4)}\" text-anchor=\"end\">{c:0}</text>");
        }
        var step = TimeSpan.FromMinutes(span > 4 * 3600 ? 60 : 30);
        var tick = new DateTime(t0.Ticks - t0.Ticks % step.Ticks + step.Ticks, DateTimeKind.Utc);
        for (; tick <= t1; tick += step)
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(tick, zone);
            sb.Append($"<text x=\"{F(X(tick))}\" y=\"{height - 10}\" text-anchor=\"middle\">{local:HH:mm}</text>");
        }
        sb.Append($"<rect x=\"{left}\" y=\"{top}\" width=\"{width - left - right}\" height=\"{height - top - bottom}\" fill=\"none\" stroke=\"#999\" />");

        var i = 0;
        foreach (var group in samples.GroupBy(s => s.Channel).OrderBy(g => g.Key))
        {
            var color = Colors[i++ % Colors.Length];
            var points = string.Join(" ", group.Select(s => $"{F(X(s.AtUtc))},{F(Y(s.Celsius))}"));
            var name = names.TryGetValue(group.Key, out var n) ? n : $"Channel {group.Key}";
            sb.Append($"<polyline fill=\"none\" stroke=\"{color}\" stroke-width=\"1.2\" points=\"{points}\"><title>{System.Net.WebUtility.HtmlEncode(name)}</title></polyline>");
        }
        sb.Append("</svg>");
        return sb.ToString();
    }
}
