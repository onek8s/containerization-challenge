using System.Globalization;
using StudyData.Core.Services;

namespace StudyData.Calculation.Development;

/// <summary>
/// Development stand-in for the Excel engine: the formulas of the FAB-ONC-201 DERIV v3 workbook written out
/// in code so the portal can be run without Excel. Other workbooks are not supported. Values are not
/// validated and must not be used for a production study.
/// </summary>
public class DevelopmentDerivationService : IDerivationService
{
    public Task<DerivationResult> DeriveAsync(DerivationRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Workbook.Code != "DERIV" || request.Study.Code != "FAB-ONC-201")
        {
            throw new DerivationException($"The development calculation has no formulas for {request.Study.Code} {request.Workbook.Code}.");
        }

        double? N(string name) =>
            request.Inputs.TryGetValue(name, out var v) && v is not null
            && double.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && d > 0
                ? d
                : null;
        var female = request.Inputs.TryGetValue("SEX", out var sex) && "F".Equals(sex as string, StringComparison.OrdinalIgnoreCase);

        var h = N("HEIGHT_CM");
        var w = N("WEIGHT_KG");
        var age = N("AGE_Y");
        var scr = N("SCR_UMOL");
        var auc = N("AUC_TARGET");
        var doseKg = N("DOSE_MG_KG");
        var given = N("CARBO_ACTUAL");

        var r = new Dictionary<string, double?>();
        r["BMI"] = h is not null && w is not null ? w / Math.Pow(h.Value / 100, 2) : null;
        r["BSA"] = h is not null && w is not null ? Math.Sqrt(h.Value * w.Value / 3600) : null;
        r["CRCL"] = age is not null && w is not null && scr is not null ? (140 - age) * w * (female ? 1.04 : 1.23) / scr : null;
        r["CRCL_CAPPED"] = r["CRCL"] is double c ? Math.Min(c, 125) : null;
        if (age is not null && scr is not null)
        {
            var k = female ? 0.7 : 0.9;
            var a = female ? -0.241 : -0.302;
            var x = scr.Value / 88.4 / k;
            r["EGFR"] = 142 * Math.Pow(Math.Min(x, 1), a) * Math.Pow(Math.Max(x, 1), -1.2) * Math.Pow(0.9938, age.Value) * (female ? 1.012 : 1);
        }
        else
        {
            r["EGFR"] = null;
        }

        r["CARBO_DOSE"] = auc is not null && r["CRCL_CAPPED"] is double cap
            ? Math.Min(Math.Round(auc.Value * (cap + 25), MidpointRounding.AwayFromZero), auc.Value * 150)
            : null;
        r["FAB_DOSE"] = doseKg is not null && w is not null
            ? Math.Min(Math.Round(doseKg.Value * w.Value / 10, MidpointRounding.AwayFromZero) * 10, 1000)
            : null;
        r["CARBO_DEV_PCT"] = r["CARBO_DOSE"] is double planned && given is not null ? (given - planned) / planned * 100 : null;

        var values = request.Outputs.ToDictionary(
            o => o,
            o => r.TryGetValue(o, out var v) && v is not null ? v.Value.ToString("R", CultureInfo.InvariantCulture) : null,
            StringComparer.OrdinalIgnoreCase);
        return Task.FromResult(new DerivationResult(values, $"{request.Workbook.Code} v{request.Workbook.Version} (development)", Array.Empty<string>()));
    }
}
