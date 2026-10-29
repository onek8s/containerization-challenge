using System.Text.RegularExpressions;
using CityPermits.Domain.Model;

namespace CityPermits.MailIntake;

public sealed record ExtractedForm(string FormCode, PermitKind Kind, double Confidence, Dictionary<string, string> Fields);

/// <summary>
/// Recognises the council's paper forms (BP1 building permit, BL2 business licence, PP1 parking
/// permit, 2019 layouts) by their form code and pulls the boxed answers by label.
/// </summary>
public static class FormReader
{
    private static readonly (string Code, PermitKind Kind, string[] Labels)[] Forms =
    {
        ("BP1", PermitKind.BuildingPermit, new[] { "Applicant name", "Site address", "Postcode", "Description of works", "Estimated cost", "Floor area", "Telephone", "Email" }),
        ("BL2", PermitKind.BusinessLicence, new[] { "Applicant name", "Trading name", "Premises address", "Postcode", "Licence type", "Opening hours", "Telephone", "Email" }),
        ("PP1", PermitKind.ParkingPermit, new[] { "Applicant name", "Address", "Postcode", "Vehicle registration", "Permit type", "Telephone", "Email" }),
    };

    public static ExtractedForm? Read(IReadOnlyList<OcrPage> pages)
    {
        var text = string.Join("\n", pages.Select(p => p.Text));
        foreach (var (code, kind, labels) in Forms)
        {
            if (!Regex.IsMatch(text, $@"\bForm\s+{code}\b", RegexOptions.IgnoreCase)) continue;
            var fields = new Dictionary<string, string>();
            foreach (var label in labels)
            {
                var m = Regex.Match(text, $@"{Regex.Escape(label)}\s*[:\-]?\s*(.+)", RegexOptions.IgnoreCase);
                if (m.Success && m.Groups[1].Value.Trim() is { Length: > 0 } v) fields[label] = v;
            }
            if (fields.TryGetValue("Vehicle registration", out var reg)) fields["Vehicle registration"] = reg.ToUpperInvariant().Replace('O', '0').Replace(" ", "");
            return new ExtractedForm(code, kind, (double)fields.Count / labels.Length, fields);
        }
        return null;
    }
}
