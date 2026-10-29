using System.Text.RegularExpressions;

namespace Proseware.ControlTower.Domain.Customs;

public static class DeclarationValidator
{
    private static readonly Regex EoriPattern = new("^[A-Z]{2}[0-9A-Z]{1,15}$", RegexOptions.Compiled);
    private static readonly Regex OfficePattern = new("^[A-Z]{2}[0-9A-Z]{6}$", RegexOptions.Compiled);
    private static readonly Regex CommodityPattern = new("^([0-9]{8}|[0-9]{10})$", RegexOptions.Compiled);

    public static IReadOnlyList<string> Validate(CustomsDeclaration d)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(d.Lrn) || d.Lrn.Length > 22) errors.Add("LRN is required (max 22 characters).");
        if (!EoriPattern.IsMatch(d.DeclarantEori ?? "")) errors.Add("Declarant EORI is invalid.");
        if (d.Type == DeclarationType.Export && string.IsNullOrEmpty(d.ConsignorEori)) errors.Add("Exporter EORI is required for export.");
        if (d.Type == DeclarationType.Import && string.IsNullOrEmpty(d.ConsigneeEori)) errors.Add("Importer EORI is required for import.");
        if (!OfficePattern.IsMatch(d.OfficeOfDeclaration ?? "")) errors.Add("Customs office of declaration is invalid.");
        if (d.Type == DeclarationType.Export && d.OfficeOfExit is { } exit && !OfficePattern.IsMatch(exit)) errors.Add("Customs office of exit is invalid.");
        if (d.Lines.Count == 0) errors.Add("At least one goods item is required.");
        if (d.Lines.Count > 999) errors.Add("At most 999 goods items are allowed.");

        foreach (var line in d.Lines)
        {
            var p = $"Item {line.LineNumber}: ";
            if (!CommodityPattern.IsMatch(line.CommodityCode)) errors.Add(p + "commodity code must have 8 or 10 digits.");
            if (string.IsNullOrWhiteSpace(line.Description)) errors.Add(p + "description is required.");
            if (line.OriginCountry.Length != 2) errors.Add(p + "country of origin must be an ISO 3166 alpha-2 code.");
            if (line.NetMassKg <= 0 || line.GrossMassKg < line.NetMassKg) errors.Add(p + "gross mass must be at least the net mass, which must be positive.");
            if (line.StatisticalValue <= 0) errors.Add(p + "statistical value must be positive.");
            if (line.ProcedureCode.Length != 4) errors.Add(p + "procedure code must have 4 digits.");
        }

        var duplicates = d.Lines.GroupBy(l => l.LineNumber).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicates.Count > 0) errors.Add("Duplicate item numbers: " + string.Join(", ", duplicates));
        return errors;
    }

    /// <summary>LRN: PF + yyMMdd + shipment id, unique per submission attempt.</summary>
    public static string NewLrn(int shipmentId, DateTimeOffset now, int attempt) =>
        $"PF{now:yyMMdd}{shipmentId:D8}{attempt:D2}";
}
