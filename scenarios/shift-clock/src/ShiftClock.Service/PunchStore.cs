using System.Globalization;
using System.Text.Json;
using ShiftClock.Shared;

namespace ShiftClock.Service;

/// <summary>
/// Punches are kept in one JSON-lines file per month; corrections and deletions are appended
/// as new lines so the file is a complete audit trail.
/// </summary>
public sealed class PunchStore
{
    private readonly string _directory;
    private readonly object _sync = new();
    private Dictionary<string, Employee> _employees = new(StringComparer.OrdinalIgnoreCase);
    private DateTime _employeesLoadedAt;

    public PunchStore(ShiftClockOptions options)
    {
        _directory = options.DataDirectory;
        Directory.CreateDirectory(Path.Combine(_directory, "punches"));
    }

    public string EmployeesFile => Path.Combine(_directory, "employees.csv");

    public Employee? FindEmployee(string badgeId)
    {
        lock (_sync)
        {
            var modified = File.Exists(EmployeesFile) ? File.GetLastWriteTimeUtc(EmployeesFile) : DateTime.MinValue;
            if (modified != _employeesLoadedAt)
            {
                _employees = LoadEmployees(EmployeesFile);
                _employeesLoadedAt = modified;
            }
            return _employees.GetValueOrDefault(badgeId);
        }
    }

    public Punch? LastPunch(string employeeNumber, DateTime now)
    {
        return Read(DateOnly.FromDateTime(now.AddDays(-1)), DateOnly.FromDateTime(now))
            .Where(p => p.EmployeeNumber == employeeNumber)
            .MaxBy(p => p.Timestamp);
    }

    public void Append(Punch punch) => AppendLine(punch.Timestamp, new StoredLine("punch", punch, null));

    public void Delete(Guid punchId, DateTime punchTimestamp, string deletedBy, string reason)
    {
        AppendLine(punchTimestamp, new StoredLine("delete", null, new Deletion(punchId, deletedBy, reason, DateTime.Now)));
    }

    public Punch? Find(Guid id, DateOnly from, DateOnly to) => Read(from, to).FirstOrDefault(p => p.Id == id);

    public List<Punch> Read(DateOnly from, DateOnly to)
    {
        var punches = new Dictionary<Guid, Punch>();
        lock (_sync)
        {
            for (var month = new DateOnly(from.Year, from.Month, 1); month <= to; month = month.AddMonths(1))
            {
                var file = MonthFile(month.Year, month.Month);
                if (!File.Exists(file))
                    continue;
                foreach (var line in File.ReadLines(file))
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;
                    var stored = JsonSerializer.Deserialize<StoredLine>(line, PipeProtocol.Json);
                    if (stored?.Punch is { } p)
                        punches[p.Id] = p;
                    else if (stored?.Deletion is { } d)
                        punches.Remove(d.PunchId);
                }
            }
        }

        return punches.Values
            .Where(p => DateOnly.FromDateTime(p.Timestamp) >= from && DateOnly.FromDateTime(p.Timestamp) <= to)
            .OrderBy(p => p.Timestamp)
            .ToList();
    }

    private void AppendLine(DateTime timestamp, StoredLine line)
    {
        var json = JsonSerializer.Serialize(line, PipeProtocol.Json);
        lock (_sync)
        {
            File.AppendAllText(MonthFile(timestamp.Year, timestamp.Month), json + Environment.NewLine);
        }
    }

    private string MonthFile(int year, int month) =>
        Path.Combine(_directory, "punches", string.Create(CultureInfo.InvariantCulture, $"{year:0000}-{month:00}.jsonl"));

    private static Dictionary<string, Employee> LoadEmployees(string path)
    {
        var result = new Dictionary<string, Employee>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path))
            return result;

        foreach (var line in File.ReadLines(path).Skip(1))
        {
            var cols = line.Split(';');
            if (cols.Length < 4)
                continue;
            result[cols[0].Trim()] = new Employee(cols[0].Trim(), cols[1].Trim(), cols[2].Trim(), cols[3].Trim());
        }
        return result;
    }

    private sealed record StoredLine(string Kind, Punch? Punch, Deletion? Deletion);

    private sealed record Deletion(Guid PunchId, string DeletedBy, string Reason, DateTime DeletedAt);
}
