using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ExpenseClaims.Api.Services;

/// <summary>System.Text.Json in .NET 6 cannot read or write <see cref="DateOnly"/>; dates travel as yyyy-MM-dd.</summary>
public sealed class DateOnlyJsonConverter : JsonConverter<DateOnly>
{
    private const string Format = "yyyy-MM-dd";

    public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (value is null)
            throw new JsonException("Date is required.");
        // The SPA's date picker sends a full ISO timestamp; only the date part is used.
        if (value.Length > Format.Length)
            value = value[..Format.Length];
        return DateOnly.ParseExact(value, Format, CultureInfo.InvariantCulture);
    }

    public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString(Format, CultureInfo.InvariantCulture));
}
