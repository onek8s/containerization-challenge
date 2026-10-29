using FieldService.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FieldService.Data.Services;

/// <summary>
/// Yearly running numbers such as WO-2024-00042 and INV-2024-0007.
/// </summary>
public class NumberSequence
{
    private readonly FieldServiceContext _db;

    public NumberSequence(FieldServiceContext db)
    {
        _db = db;
    }

    public async Task<string> NextAsync(string name, string prefix, int width, DateTime nowUtc, CancellationToken ct = default)
    {
        var year = nowUtc.Year;
        var counter = await _db.SequenceCounters.SingleOrDefaultAsync(s => s.Name == name && s.Year == year, ct);
        if (counter == null)
        {
            counter = new SequenceCounter { Name = name, Year = year, Value = 0 };
            _db.SequenceCounters.Add(counter);
        }

        counter.Value++;
        return Format(prefix, year, counter.Value, width);
    }

    public static string Format(string prefix, int year, int value, int width) =>
        $"{prefix}-{year}-{value.ToString().PadLeft(width, '0')}";
}
