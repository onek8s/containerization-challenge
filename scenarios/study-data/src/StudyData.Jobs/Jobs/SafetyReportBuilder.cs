using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StudyData.Calculation;
using StudyData.Core.Coding;
using StudyData.Core.Design;
using StudyData.Core.Services;
using StudyData.Core.Subjects;
using StudyData.Data;
using StudyData.Integration;

namespace StudyData.Jobs.Jobs;

/// <summary>
/// DSMB safety workbook: adverse event listing, exposure and lab shifts written into the study's validated
/// template, recalculated in Excel and exported to PDF. Arms are coded X / Y for the open DSMB session.
/// </summary>
public class SafetyReportBuilder
{
    private readonly StudyDataContext _db;
    private readonly IReportWorkbookRunner _runner;
    private readonly WorkbookStore _store;
    private readonly JobsOptions _options;
    private readonly ExportOptions _exports;
    private readonly IClock _clock;

    public SafetyReportBuilder(StudyDataContext db, IReportWorkbookRunner runner, WorkbookStore store, IOptions<JobsOptions> options,
        IOptions<ExportOptions> exports, IClock clock)
    {
        _db = db;
        _runner = runner;
        _store = store;
        _options = options.Value;
        _exports = exports.Value;
        _clock = clock;
    }

    public async Task<string> BuildAsync(Study study, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var subjects = await _db.Subjects.Where(s => s.StudyId == study.Id && s.ArmId != null)
            .Include(s => s.Site).Include(s => s.Arm)
            .Include(s => s.Visits).ThenInclude(v => v.Forms).ThenInclude(f => f.Values)
            .AsSplitQuery().ToListAsync(ct);
        var coding = await _db.Coding.Where(c => subjects.Select(s => s.Id).Contains(c.SubjectId) && c.Dictionary == "MEDDRA").ToListAsync(ct);
        var labs = await _db.LabResults.Where(l => l.StudyId == study.Id).ToListAsync(ct);
        var ae = study.Forms.First(f => f.Code == "AE");
        var ex = study.Forms.First(f => f.Code == "EX");

        var aeRows = new List<object?[]>();
        var exposure = new List<object?[]>();
        var shifts = new List<object?[]>();
        foreach (var s in subjects.OrderBy(s => s.Number))
        {
            var arm = CodedArm(study, s.Arm!.Code);
            foreach (var f in s.Visits.SelectMany(v => v.Forms).Where(f => f.FormDefinitionId == ae.Id && f.Values.Count > 0).OrderBy(f => f.Row))
            {
                string? V(string code) => f.Values.FirstOrDefault(v => v.FieldCode == code)?.Value;
                var code = coding.FirstOrDefault(c => c.FormInstanceId == f.Id && c.FieldCode == "AETERM");
                var (pt, soc) = Split(code);
                aeRows.Add(new object?[]
                {
                    s.Number, arm, s.Site!.Number, V("AETERM"), pt ?? "(not coded)", soc ?? "Other",
                    int.TryParse(V("AETOXGR"), out var g) ? g : null, V("AESER") == "1" ? "Y" : "N", V("AEREL") == "1" ? "Y" : "N",
                    V("AESTDAT"), V("AEENDAT"), V("AEOUT")
                });
            }

            var doses = s.Visits.SelectMany(v => v.Forms).Where(f => f.FormDefinitionId == ex.Id && f.Values.Count > 0).ToList();
            if (doses.Count > 0)
            {
                decimal Sum(string code) => doses.Sum(d => decimal.TryParse(d.Values.FirstOrDefault(v => v.FieldCode == code)?.Value,
                    System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var x) ? x : 0);
                exposure.Add(new object?[]
                {
                    s.Number, arm, doses.Count, Sum("EXCARBO"), Sum("EXFAB"),
                    doses.Count(d => d.Values.FirstOrDefault(v => v.FieldCode == "EXADJ")?.Value == "1")
                });
            }

            foreach (var test in labs.Where(l => l.SubjectId == s.Id).GroupBy(l => l.TestCode))
            {
                var baseline = test.Where(l => l.VisitCode is "SCR" or "C1D1").OrderBy(l => l.CollectedAt).FirstOrDefault();
                var worst = test.OrderByDescending(l => l.Grade ?? 0).ThenBy(l => l.CollectedAt).First();
                shifts.Add(new object?[] { s.Number, arm, test.First().TestName, baseline?.Value, worst.Value, worst.Grade, worst.VisitCode });
            }
        }

        var template = _store.SourcePath(study, "reports", _options.SafetyTemplate);
        var folder = Path.Combine(_exports.OutputRoot, study.Code, "Safety", now.ToString("yyyy-MM-dd"));
        var stem = Path.Combine(folder, $"{study.Code}_DSMB_safety_{now:yyyyMMdd}");
        var result = await _runner.RunAsync(new ReportWorkbookRequest(
            template, stem + ".xlsx", stem + ".pdf",
            new Dictionary<string, object?> { ["CUTOFF"] = now.ToString("dd-MMM-yyyy") },
            new Dictionary<string, IReadOnlyList<object?[]>>
            {
                ["AE_START"] = aeRows,
                ["EXPOSURE_START"] = exposure,
                ["LAB_START"] = shifts
            },
            new[] { "Summary" },
            TimeSpan.FromMinutes(5)), ct);
        return result.Pdf ?? result.Workbook;
    }

    /// <summary>Stable per-study coding of arms so that the DSMB open report never shows real arm codes.</summary>
    public static string CodedArm(Study study, string armCode)
    {
        var flip = study.Code.Sum(c => c) % 2 == 1;
        var first = study.Arms.OrderBy(a => a.Code).First().Code == armCode;
        return first ^ flip ? "X" : "Y";
    }

    private static (string? Pt, string? Soc) Split(CodingItem? item)
    {
        if (item?.Hierarchy is null)
        {
            return (item?.Term, null);
        }

        // "PTCODE PT term | SOC name"
        var parts = item.Hierarchy.Split('|', 2, StringSplitOptions.TrimEntries);
        var pt = parts[0].Contains(' ') ? parts[0][(parts[0].IndexOf(' ') + 1)..] : parts[0];
        return (pt, parts.Length > 1 ? parts[1] : null);
    }
}
