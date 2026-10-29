using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StudyData.Core.Documents;
using StudyData.Core.Labs;
using StudyData.Core.Services;
using StudyData.Data;

namespace StudyData.Integration.Labs;

public record LabImportResult(string File, int Imported, int Duplicates, int Rejected, IReadOnlyList<string> Errors);

/// <summary>
/// Central lab transfer format (semicolon separated, UTF-8, header row):
/// STUDY;SITE;SUBJECT;VISIT;ACCESSION;COLLECTED;TESTCD;TEST;RESULT;UNIT;LOW;HIGH
/// COLLECTED is ISO 8601 local time of the site.
/// </summary>
public class LabImportService
{
    private static readonly string[] Header = { "STUDY", "SITE", "SUBJECT", "VISIT", "ACCESSION", "COLLECTED", "TESTCD", "TEST", "RESULT", "UNIT", "LOW", "HIGH" };

    private readonly StudyDataContext _db;
    private readonly IClock _clock;
    private readonly ILogger<LabImportService> _log;

    public LabImportService(StudyDataContext db, IClock clock, ILogger<LabImportService> log)
    {
        _db = db;
        _clock = clock;
        _log = log;
    }

    public async Task<LabImportResult> ImportAsync(LabFile file, CancellationToken ct = default)
    {
        var hash = Convert.ToHexString(SHA256.HashData(file.Content)).ToLowerInvariant();
        if (await _db.LabFiles.AnyAsync(f => f.Sha256 == hash, ct))
        {
            return new LabImportResult(file.Name, 0, 0, 0, new[] { "File was already imported." });
        }

        var errors = new List<string>();
        var lines = Encoding.UTF8.GetString(file.Content).TrimStart('\uFEFF').Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0).ToList();
        if (lines.Count == 0 || !lines[0].Split(';').Select(h => h.Trim().ToUpperInvariant()).SequenceEqual(Header))
        {
            errors.Add("Unexpected header.");
            return await Record(0, lines.Count, 0);
        }

        var studies = await _db.Studies.ToDictionaryAsync(s => s.Code, ct);
        var subjects = await _db.Subjects.Include(s => s.Site).ToListAsync(ct);
        var imported = 0;
        var duplicates = 0;
        var rejected = 0;
        var now = _clock.UtcNow;

        for (var i = 1; i < lines.Count; i++)
        {
            var p = lines[i].Split(';');
            if (p.Length != Header.Length)
            {
                errors.Add($"Line {i + 1}: {p.Length} columns.");
                rejected++;
                continue;
            }

            if (!studies.TryGetValue(p[0].Trim(), out var study))
            {
                errors.Add($"Line {i + 1}: unknown study {p[0]}.");
                rejected++;
                continue;
            }

            var subject = subjects.FirstOrDefault(s => s.StudyId == study.Id && s.Number == p[2].Trim());
            if (subject is null || subject.Site!.Number != p[1].Trim())
            {
                errors.Add($"Line {i + 1}: subject {p[2]} is not enrolled at site {p[1]}.");
                rejected++;
                continue;
            }

            if (!DateTime.TryParse(p[5], CultureInfo.InvariantCulture, DateTimeStyles.None, out var collectedLocal))
            {
                errors.Add($"Line {i + 1}: invalid collection time {p[5]}.");
                rejected++;
                continue;
            }

            var accession = p[4].Trim();
            var testCode = p[6].Trim().ToUpperInvariant();
            if (await _db.LabResults.AnyAsync(r => r.Accession == accession && r.TestCode == testCode, ct)
                || _db.LabResults.Local.Any(r => r.Accession == accession && r.TestCode == testCode))
            {
                duplicates++;
                continue;
            }

            var low = Dec(p[10]);
            var high = Dec(p[11]);
            var (flag, grade) = LabFlags.Evaluate(testCode, p[8].Trim(), low, high);
            var zone = FindZone(subject.Site.TimeZone);
            var result = new LabResult
            {
                StudyId = study.Id, SubjectId = subject.Id, VisitCode = p[3].Trim(), Accession = accession,
                CollectedAt = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(collectedLocal, DateTimeKind.Unspecified), zone),
                TestCode = testCode, TestName = p[7].Trim(), Value = p[8].Trim(), Unit = p[9].Trim(),
                LowerLimit = low, UpperLimit = high, Flag = flag, Grade = grade, SourceFile = file.Name, ImportedAt = now
            };
            _db.LabResults.Add(result);
            imported++;

            if (grade >= 3)
            {
                _db.Notifications.Add(new Notification
                {
                    At = now, StudyId = study.Id, SiteId = subject.SiteId, SubjectId = subject.Id, Kind = "CriticalLab",
                    Text = $"{subject.Number} {result.VisitCode}: {result.TestName} {result.Value} {result.Unit} (grade {grade})"
                });
            }
        }

        return await Record(imported, rejected, duplicates);

        async Task<LabImportResult> Record(int ok, int bad, int dup)
        {
            _db.LabFiles.Add(new LabTransferFile
            {
                FileName = file.Name, Sha256 = hash, ImportedAt = _clock.UtcNow, Rows = ok, Rejected = bad,
                Errors = errors.Count == 0 ? null : string.Join('\n', errors.Take(200))
            });
            await _db.SaveChangesAsync(ct);
            _log.LogInformation("Lab file {File}: {Imported} imported, {Duplicates} duplicates, {Rejected} rejected", file.Name, ok, dup, bad);
            return new LabImportResult(file.Name, ok, dup, bad, errors);
        }
    }

    private static decimal? Dec(string s) =>
        decimal.TryParse(s.Trim().Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : null;

    /// <summary>Site time zones are stored as IANA ids; Windows ids are accepted too.</summary>
    public static TimeZoneInfo FindZone(string id)
    {
        if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone))
        {
            return zone;
        }

        if (TimeZoneInfo.TryConvertIanaIdToWindowsId(id, out var windows) && TimeZoneInfo.TryFindSystemTimeZoneById(windows, out zone))
        {
            return zone;
        }

        return TimeZoneInfo.Utc;
    }
}
