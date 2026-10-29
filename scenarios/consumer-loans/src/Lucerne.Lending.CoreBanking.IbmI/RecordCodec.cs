using System.Globalization;
using System.Runtime.Versioning;

namespace Lucerne.Lending.CoreBanking.IbmI;

/// <summary>
/// Builds and reads RPG parameter buffers with the cwbx converters (EBCDIC CCSID 278, packed and zoned decimals).
/// Converter objects are apartment bound, so a codec belongs to one <see cref="CwbxWorker"/> thread.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class RecordCodec : IDisposable
{
    private readonly dynamic _string;
    private readonly dynamic _packed;
    private readonly dynamic _zoned;

    public RecordCodec(int codePage)
    {
        _string = Cwbx.Create(Cwbx.StringConverter);
        _string.CodePage = codePage;
        _packed = Cwbx.Create(Cwbx.PackedConverter);
        _zoned = Cwbx.Create(Cwbx.ZonedConverter);
    }

    public byte[] Encode(RecordLayout layout, IReadOnlyDictionary<string, object?> values)
    {
        var buffer = new byte[layout.Length];
        foreach (var field in layout.Fields)
        {
            values.TryGetValue(field.Name, out var value);
            var bytes = EncodeField(field, value);
            Buffer.BlockCopy(bytes, 0, buffer, layout.OffsetOf(field.Name), field.ByteLength);
        }
        return buffer;
    }

    public Dictionary<string, string> Decode(RecordLayout layout, byte[] buffer)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in layout.Fields)
        {
            var slice = new byte[field.ByteLength];
            Buffer.BlockCopy(buffer, layout.OffsetOf(field.Name), slice, 0, field.ByteLength);
            result[field.Name] = DecodeField(field, slice);
        }
        return result;
    }

    private byte[] EncodeField(FieldSpec field, object? value)
    {
        switch (field.Type)
        {
            case FieldType.Char:
                _string.Length = field.Length;
                var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
                if (text.Length > field.Length) text = text[..field.Length];
                return (byte[])_string.ToBytes(text.PadRight(field.Length));
            case FieldType.Packed:
                _packed.Digits = field.Length;
                _packed.DecimalPosition = field.Decimals;
                return (byte[])_packed.ToBytes(FormatNumber(value, field.Decimals));
            case FieldType.Zoned:
                _zoned.Digits = field.Length;
                _zoned.DecimalPosition = field.Decimals;
                return (byte[])_zoned.ToBytes(FormatNumber(value, field.Decimals));
            default:
                throw new NotSupportedException(field.Type.ToString());
        }
    }

    private string DecodeField(FieldSpec field, byte[] bytes)
    {
        switch (field.Type)
        {
            case FieldType.Char:
                _string.Length = field.Length;
                return ((string)_string.FromBytes(bytes)).TrimEnd();
            case FieldType.Packed:
                _packed.Digits = field.Length;
                _packed.DecimalPosition = field.Decimals;
                return (string)_packed.FromBytes(bytes);
            default:
                _zoned.Digits = field.Length;
                _zoned.DecimalPosition = field.Decimals;
                return (string)_zoned.FromBytes(bytes);
        }
    }

    private static string FormatNumber(object? value, int decimals) => value switch
    {
        null => "0",
        DateOnly d => d.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
        decimal m => Math.Round(m, decimals).ToString(CultureInfo.InvariantCulture),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "0"
    };

    public void Dispose()
    {
        Cwbx.Release(_string);
        Cwbx.Release(_packed);
        Cwbx.Release(_zoned);
    }
}

internal static class Fields
{
    public static decimal Dec(this IReadOnlyDictionary<string, string> f, string name) =>
        decimal.TryParse(f[name], NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : 0m;

    public static int Int(this IReadOnlyDictionary<string, string> f, string name) => (int)f.Dec(name);

    public static DateOnly? Date(this IReadOnlyDictionary<string, string> f, string name)
    {
        var digits = ((long)f.Dec(name)).ToString(CultureInfo.InvariantCulture);
        return digits.Length == 8 && DateOnly.TryParseExact(digits, "yyyyMMdd", out var d) ? d : null;
    }

    public static bool Flag(this IReadOnlyDictionary<string, string> f, string name) => f[name] == "Y";
}
