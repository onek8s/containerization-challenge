using System.Globalization;
using System.Text;
using PolicyChanges.Core.Requests;

namespace PolicyChanges.Core.Flows;

/// <summary>
/// Expands <c>{name[:argument][|filter]}</c> placeholders: <c>Policy</c>, <c>Reference</c>,
/// <c>Effective[:format]</c>, <c>Field:Name</c>, <c>Var:Name</c>, <c>Item:index</c>, <c>Count:Name</c>.
/// Filters: <c>iban</c>, <c>date</c> (yyyy-MM-dd to dd.MM.yyyy), <c>upper</c>, <c>trim</c>.
/// </summary>
public static class FlowTemplate
{
    public const string PolicyDeskDateFormat = "dd.MM.yyyy";

    public static string Expand(string? template, FlowContext context)
    {
        if (string.IsNullOrEmpty(template)) return "";
        var sb = new StringBuilder();
        var i = 0;
        while (i < template.Length)
        {
            var open = template.IndexOf('{', i);
            if (open < 0)
            {
                sb.Append(template, i, template.Length - i);
                break;
            }
            sb.Append(template, i, open - i);
            if (open + 1 < template.Length && template[open + 1] == '{')
            {
                sb.Append('{');
                i = open + 2;
                continue;
            }
            var close = template.IndexOf('}', open);
            if (close < 0) throw new FormatException($"Unclosed placeholder in '{template}'.");
            sb.Append(Resolve(template[(open + 1)..close], context));
            i = close + 1;
        }
        return sb.ToString();
    }

    private static string Resolve(string expression, FlowContext context)
    {
        var filters = expression.Split('|');
        var head = filters[0];
        var colon = head.IndexOf(':');
        var name = colon < 0 ? head : head[..colon];
        var arg = colon < 0 ? null : head[(colon + 1)..];

        var value = name switch
        {
            "Policy" => context.Request.PolicyNumber,
            "Reference" => context.Request.Reference,
            "Effective" => context.Request.EffectiveDate.ToString(arg ?? PolicyDeskDateFormat, CultureInfo.InvariantCulture),
            "Field" => context.Request.Field(arg ?? ""),
            "Var" => context.Variables.TryGetValue(arg ?? "", out var v) ? v : throw new FormatException($"Variable {arg} is not set."),
            "Item" => Item(context, arg),
            "Count" => Lines(context.Request.Field(arg ?? "")).Count.ToString(CultureInfo.InvariantCulture),
            _ => throw new FormatException($"Unknown placeholder {{{expression}}}."),
        };

        foreach (var filter in filters.Skip(1))
        {
            value = filter switch
            {
                "iban" => Iban.Format(value),
                "date" => DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture).ToString(PolicyDeskDateFormat, CultureInfo.InvariantCulture),
                "upper" => value.ToUpperInvariant(),
                "trim" => value.Trim(),
                _ => throw new FormatException($"Unknown filter {filter}."),
            };
        }
        return value;
    }

    private static string Item(FlowContext context, string? arg)
    {
        if (context.Item is null) throw new FormatException("{Item} is only valid inside forEach.");
        if (arg is null) return string.Join('|', context.Item);
        var index = int.Parse(arg, CultureInfo.InvariantCulture);
        return index < context.Item.Length ? context.Item[index] : "";
    }

    public static IReadOnlyList<string> Lines(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
