using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;

namespace VanArsdel.Notifications.Sending;

public record RenderedMail(string Subject, string Html);

/// <summary>
/// Templates are embedded HTML files "{name}.{lang}.html" whose first line is "Subject: ...".
/// Missing languages fall back to English. Marketing owns the wording; developers own the placeholders.
/// </summary>
public class TemplateRenderer
{
    private static readonly Regex Placeholder = new(@"\{\{(\w+)\}\}", RegexOptions.Compiled);
    private readonly Assembly _assembly = typeof(TemplateRenderer).Assembly;

    public static string LanguageFor(string market) => (market ?? "").ToUpperInvariant() switch
    {
        "FI" => "fi",
        "SE" => "sv",
        _ => "en",
    };

    public RenderedMail Render(string template, string language, IDictionary<string, object> values)
    {
        var text = Load(template, language) ?? Load(template, "en") ?? throw new InvalidOperationException("Template " + template + " missing");
        var newline = text.IndexOf('\n');
        var subject = text.Substring(0, newline).Replace("Subject:", "").Trim();
        var body = text.Substring(newline + 1);
        return new RenderedMail(Fill(subject, values, false), Fill(body, values, true));
    }

    private static string Fill(string text, IDictionary<string, object> values, bool html) =>
        Placeholder.Replace(text, m =>
        {
            if (!values.TryGetValue(m.Groups[1].Value, out var value) || value == null) return "";
            var s = value switch
            {
                decimal d => d.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                DateTime dt => dt.ToString("yyyy-MM-dd"),
                _ => value.ToString(),
            };
            return html ? WebUtility.HtmlEncode(s) : s;
        });

    private string Load(string template, string language)
    {
        using var stream = _assembly.GetManifestResourceStream($"VanArsdel.Notifications.Templates.{template}.{language}.html");
        if (stream == null) return null;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
