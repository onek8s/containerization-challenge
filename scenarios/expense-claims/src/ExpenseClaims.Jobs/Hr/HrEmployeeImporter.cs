using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using ExpenseClaims.Core.Payments;
using ExpenseClaims.Data;
using ExpenseClaims.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExpenseClaims.Jobs.Hr;

public sealed record HrImportResult(int Added, int Updated, int Deactivated, IReadOnlyList<string> Rejected);

/// <summary>Synchronises the employee register with a full export from the HR system.</summary>
public sealed class HrEmployeeImporter
{
    private readonly ExpenseClaimsDbContext _db;
    private readonly ILogger<HrEmployeeImporter> _logger;

    public HrEmployeeImporter(ExpenseClaimsDbContext db, ILogger<HrEmployeeImporter> logger)
    {
        _db = db;
        _logger = logger;
    }

    public static IReadOnlyList<EmployeeRecord> Read(Stream csv)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = ";",
            TrimOptions = TrimOptions.Trim,
            MissingFieldFound = null
        };
        using var reader = new StreamReader(csv, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        using var parser = new CsvReader(reader, config);
        return parser.GetRecords<EmployeeRecord>().ToList();
    }

    public async Task<HrImportResult> ImportAsync(IReadOnlyList<EmployeeRecord> records, double maxDeactivationShare, DateTime today, CancellationToken ct)
    {
        var rejected = new List<string>();
        var valid = new Dictionary<string, EmployeeRecord>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in records)
        {
            if (string.IsNullOrWhiteSpace(r.EmployeeNumber) || string.IsNullOrWhiteSpace(r.Upn) || !r.Upn.Contains('@'))
            {
                rejected.Add($"{r.EmployeeNumber}: personnel number and UPN are required");
                continue;
            }
            if (!string.IsNullOrWhiteSpace(r.Iban) && !Iban.IsValid(r.Iban))
                rejected.Add($"{r.EmployeeNumber}: invalid bank account, imported without it");
            if (!valid.TryAdd(r.EmployeeNumber, r))
                rejected.Add($"{r.EmployeeNumber}: duplicate row");
        }

        var existing = await _db.Employees.ToDictionaryAsync(e => e.EmployeeNumber, StringComparer.OrdinalIgnoreCase, ct);
        var leaving = existing.Values.Where(e => e.Active && (!valid.TryGetValue(e.EmployeeNumber, out var r) || r.EmploymentEnd < today)).ToList();
        var active = existing.Values.Count(e => e.Active);
        if (active > 20 && leaving.Count > active * maxDeactivationShare)
            throw new InvalidOperationException(
                $"HR export would deactivate {leaving.Count} of {active} employees. The file is probably incomplete; import stopped.");

        int added = 0, updated = 0;
        var now = DateTime.Now;
        foreach (var r in valid.Values)
        {
            if (!existing.TryGetValue(r.EmployeeNumber, out var e))
            {
                e = new Employee { EmployeeNumber = r.EmployeeNumber };
                _db.Employees.Add(e);
                added++;
            }
            else
            {
                updated++;
            }

            e.Upn = r.Upn.Trim().ToLowerInvariant();
            e.DisplayName = $"{r.FirstName} {r.LastName}".Trim();
            e.Email = string.IsNullOrWhiteSpace(r.Email) ? e.Upn : r.Email.Trim();
            e.CostCenter = r.CostCenter;
            e.ManagerEmployeeNumber = string.IsNullOrWhiteSpace(r.ManagerEmployeeNumber) ? null : r.ManagerEmployeeNumber;
            e.Iban = Iban.IsValid(r.Iban) ? Iban.Normalize(r.Iban!) : null;
            e.Bic = string.IsNullOrWhiteSpace(r.Bic) ? null : r.Bic.Trim().ToUpperInvariant();
            e.Active = r.EmploymentEnd is null || r.EmploymentEnd >= today;
            e.UpdatedAt = now;
        }

        foreach (var e in leaving.Where(e => !valid.ContainsKey(e.EmployeeNumber)))
        {
            e.Active = false;
            e.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("HR import: {Added} added, {Updated} updated, {Deactivated} deactivated, {Rejected} rejected",
            added, updated, leaving.Count, rejected.Count);
        return new HrImportResult(added, updated, leaving.Count, rejected);
    }
}
