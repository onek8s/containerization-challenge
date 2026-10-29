using System.Collections.Concurrent;
using System.Globalization;
using StudyData.Core.Design;

namespace StudyData.Core.EditChecks;

public record CheckResult(string Code, string FieldCode, EditCheckSeverity Severity, string Message);

/// <summary>Field values of the form being saved, falling back to other forms of the subject for FORM.FIELD names.</summary>
public sealed class FormFieldResolver : IFieldResolver
{
    private readonly FormDefinition _form;
    private readonly IReadOnlyDictionary<string, string?> _values;
    private readonly Func<string, string, (FieldType? Type, string? Value)>? _otherForms;

    public FormFieldResolver(FormDefinition form, IReadOnlyDictionary<string, string?> values,
        Func<string, string, (FieldType? Type, string? Value)>? otherForms = null)
    {
        _form = form;
        _values = values;
        _otherForms = otherForms;
    }

    public string? Resolve(string name)
    {
        var (form, field) = Split(name);
        if (form is null || string.Equals(form, _form.Code, StringComparison.OrdinalIgnoreCase))
        {
            return _values.TryGetValue(field, out var v) ? v : null;
        }

        return _otherForms?.Invoke(form, field).Value;
    }

    public FieldType? TypeOf(string name)
    {
        var (form, field) = Split(name);
        if (form is null || string.Equals(form, _form.Code, StringComparison.OrdinalIgnoreCase))
        {
            return _form.Fields.FirstOrDefault(f => string.Equals(f.Code, field, StringComparison.OrdinalIgnoreCase))?.Type;
        }

        return _otherForms?.Invoke(form, field).Type;
    }

    private static (string? Form, string Field) Split(string name)
    {
        var i = name.IndexOf('.');
        return i < 0 ? (null, name) : (name[..i], name[(i + 1)..]);
    }
}

public static class EditCheckRunner
{
    private static readonly ConcurrentDictionary<string, Node> Cache = new();

    public static Node Compile(string expression) => Cache.GetOrAdd(expression, Parser.Parse);

    /// <summary>Validates an expression against a form design; returns the error or null.</summary>
    public static string? Validate(string expression, FormDefinition form)
    {
        try
        {
            var node = Parser.Parse(expression);
            var resolver = new FormFieldResolver(form, new Dictionary<string, string?>(), (_, _) => (FieldType.Text, null));
            new Evaluator(resolver, DateOnly.FromDateTime(DateTime.Today)).Evaluate(node);
            return null;
        }
        catch (ExpressionException ex)
        {
            return ex.Message;
        }
    }

    /// <summary>Built-in checks (required, type, range, choice) followed by the form's edit checks.</summary>
    public static List<CheckResult> Run(FormDefinition form, IFieldResolver resolver, IReadOnlyDictionary<string, string?> values, DateOnly today)
    {
        var results = new List<CheckResult>();
        foreach (var field in form.Fields.Where(f => f.Type != FieldType.Derived))
        {
            values.TryGetValue(field.Code, out var raw);
            results.AddRange(FieldChecks(field, raw));
        }

        var evaluator = new Evaluator(resolver, today);
        foreach (var check in form.EditChecks.Where(c => c.Active))
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(check.When) && evaluator.Evaluate(Compile(check.When)) is not true)
                {
                    continue;
                }

                if (evaluator.Evaluate(Compile(check.Expression)) is false)
                {
                    results.Add(new CheckResult(check.Code, check.FieldCode, check.Severity, check.Message));
                }
            }
            catch (ExpressionException ex)
            {
                results.Add(new CheckResult(check.Code, check.FieldCode, EditCheckSeverity.Warning,
                    $"Edit check {check.Code} could not run: {ex.Message}"));
            }
        }

        return results;
    }

    public static IEnumerable<CheckResult> FieldChecks(FieldDefinition field, string? raw)
    {
        var code = "SYS." + field.Code;
        if (string.IsNullOrWhiteSpace(raw))
        {
            if (field.Required)
            {
                yield return new CheckResult(code + ".REQ", field.Code, EditCheckSeverity.Query, $"{field.Label} is required.");
            }

            yield break;
        }

        switch (field.Type)
        {
            case FieldType.Integer:
            case FieldType.Decimal:
                if (!decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var d)
                    || (field.Type == FieldType.Integer && d != decimal.Truncate(d)))
                {
                    yield return new CheckResult(code + ".TYPE", field.Code, EditCheckSeverity.Hard,
                        $"{field.Label} must be a {(field.Type == FieldType.Integer ? "whole number" : "number")}.");
                    yield break;
                }

                if (field.Decimals is int places && decimal.Round(d, places) != d)
                {
                    yield return new CheckResult(code + ".DEC", field.Code, EditCheckSeverity.Hard,
                        $"{field.Label} allows {places} decimal(s).");
                }

                if ((field.Min is not null && d < field.Min) || (field.Max is not null && d > field.Max))
                {
                    yield return new CheckResult(code + ".RANGE", field.Code, EditCheckSeverity.Query,
                        $"{field.Label} {d.ToString(CultureInfo.InvariantCulture)} is outside the expected range {field.Min?.ToString(CultureInfo.InvariantCulture)}-{field.Max?.ToString(CultureInfo.InvariantCulture)} {field.Unit}.".TrimEnd());
                }

                break;
            case FieldType.Date:
                if (!DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                {
                    yield return new CheckResult(code + ".TYPE", field.Code, EditCheckSeverity.Hard, $"{field.Label} must be a date (yyyy-mm-dd).");
                }

                break;
            case FieldType.Time:
                if (!TimeOnly.TryParseExact(raw, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                {
                    yield return new CheckResult(code + ".TYPE", field.Code, EditCheckSeverity.Hard, $"{field.Label} must be a time (hh:mm).");
                }

                break;
            case FieldType.Choice:
                if (field.ChoiceList().All(c => c.Key != raw))
                {
                    yield return new CheckResult(code + ".CHOICE", field.Code, EditCheckSeverity.Hard, $"'{raw}' is not an option for {field.Label}.");
                }

                break;
            case FieldType.Text:
                if (field.MaxLength is int max && raw.Length > max)
                {
                    yield return new CheckResult(code + ".LEN", field.Code, EditCheckSeverity.Hard, $"{field.Label} allows {max} characters.");
                }

                break;
        }
    }
}
