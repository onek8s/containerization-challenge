using CityPermits.Domain.Model;
using CityPermits.Domain.Workflow;
using Microsoft.EntityFrameworkCore;

namespace CityPermits.Data;

/// <summary>Allocates case references from the Sequences table (one counter per prefix and year).</summary>
public sealed class ReferenceAllocator
{
    private readonly PermitsDbContext _db;

    public ReferenceAllocator(PermitsDbContext db) => _db = db;

    public async Task<string> NextAsync(PermitKind kind, int year, CancellationToken ct = default)
    {
        var name = ReferenceNumbers.Prefix(kind);
        int value;
        if (_db.IsSqlServer)
        {
            value = await _db.Database.SqlQueryRaw<int>(
                @"UPDATE permits.Sequences WITH (UPDLOCK, HOLDLOCK) SET Value = Value + 1 OUTPUT inserted.Value AS [Value] WHERE Name = {0} AND [Year] = {1}",
                name, year).ToListAsync(ct).ContinueWith(t => t.Result.FirstOrDefault(), ct);
            if (value == 0)
            {
                _db.Sequences.Add(new SequenceCounter { Name = name, Year = year, Value = 1 });
                await _db.SaveChangesAsync(ct);
                value = 1;
            }
        }
        else
        {
            var row = await _db.Sequences.FirstOrDefaultAsync(s => s.Name == name && s.Year == year, ct);
            if (row is null)
            {
                row = new SequenceCounter { Name = name, Year = year, Value = 0 };
                _db.Sequences.Add(row);
            }
            row.Value++;
            await _db.SaveChangesAsync(ct);
            value = row.Value;
        }
        return ReferenceNumbers.Format(kind, year, value);
    }

    public async Task<string> NextSerialAsync(string name, int year, string format, CancellationToken ct = default)
    {
        var row = await _db.Sequences.FirstOrDefaultAsync(s => s.Name == name && s.Year == year, ct);
        if (row is null)
        {
            row = new SequenceCounter { Name = name, Year = year, Value = 0 };
            _db.Sequences.Add(row);
        }
        row.Value++;
        await _db.SaveChangesAsync(ct);
        return string.Format(format, year, row.Value);
    }
}
