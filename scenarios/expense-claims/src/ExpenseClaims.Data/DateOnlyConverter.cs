using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ExpenseClaims.Data;

/// <summary>EF Core 6 has no built-in mapping for <see cref="DateOnly"/>.</summary>
public sealed class DateOnlyConverter : ValueConverter<DateOnly, DateTime>
{
    public DateOnlyConverter()
        : base(d => d.ToDateTime(TimeOnly.MinValue), d => DateOnly.FromDateTime(d))
    {
    }
}
