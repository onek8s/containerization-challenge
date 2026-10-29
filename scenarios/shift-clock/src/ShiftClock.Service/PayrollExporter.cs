using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;
using ShiftClock.Shared;

namespace ShiftClock.Service;

/// <summary>Writes the semicolon separated hours file imported by the payroll system.</summary>
public sealed class PayrollExporter
{
    private readonly ShiftClockOptions _options;

    public PayrollExporter(IOptions<ShiftClockOptions> options) => _options = options.Value;

    public string Export(IReadOnlyList<Punch> punches, DateOnly from, DateOnly to, string exportedBy)
    {
        Directory.CreateDirectory(_options.PayrollExportPath);
        var fileName = string.Create(CultureInfo.InvariantCulture,
            $"{_options.Terminal}_{from:yyyyMMdd}_{to:yyyyMMdd}.csv");
        var path = Path.Combine(_options.PayrollExportPath, fileName);

        var sb = new StringBuilder();
        sb.AppendLine("EmployeeNumber;Name;Hours;UnpairedPunches;PeriodStart;PeriodEnd;ExportedBy");
        foreach (var row in PunchRules.Summarize(punches))
        {
            sb.AppendLine(string.Join(';',
                row.EmployeeNumber,
                row.EmployeeName,
                row.Hours.ToString("0.00", CultureInfo.InvariantCulture),
                row.UnpairedPunches.ToString(CultureInfo.InvariantCulture),
                from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                exportedBy));
        }

        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        return path;
    }
}
