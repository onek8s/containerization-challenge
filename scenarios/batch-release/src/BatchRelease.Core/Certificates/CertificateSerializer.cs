using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace BatchRelease.Core.Certificates;

/// <summary>
/// Writes certificate content as canonical JSON: fixed property order, no whitespace,
/// invariant formats, documents sorted by kind and file name. The same content always
/// produces the same bytes, so the signature can be checked against a re-serialized copy.
/// </summary>
public static class CertificateSerializer
{
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static byte[] Serialize(ReleaseCertificateContent c)
    {
        using var buffer = new MemoryStream();
        using (var w = new Utf8JsonWriter(buffer, WriterOptions))
        {
            w.WriteStartObject();
            w.WriteString("schema", c.SchemaVersion);
            w.WriteString("certificateNumber", c.CertificateNumber);
            w.WriteString("decision", c.Decision);
            w.WriteString("manufacturer", c.Manufacturer);
            w.WriteString("site", c.SiteCode);
            w.WriteStartObject("batch");
            w.WriteString("number", c.BatchNumber);
            w.WriteString("productCode", c.ProductCode);
            w.WriteString("productName", c.ProductName);
            w.WriteString("strength", c.Strength);
            w.WriteString("dosageForm", c.DosageForm);
            w.WriteString("quantity", c.Quantity.ToString("0.###", CultureInfo.InvariantCulture));
            w.WriteString("unit", c.Unit);
            w.WriteString("manufactured", c.ManufacturedOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            w.WriteString("expiry", c.ExpiresOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            w.WriteEndObject();
            w.WriteStartObject("review");
            w.WriteString("by", c.ReviewedBy);
            if (c.ReviewedUtc is { } reviewed)
                w.WriteString("at", Utc(reviewed));
            else
                w.WriteNull("at");
            w.WriteEndObject();
            w.WriteStartObject("signature");
            w.WriteString("qualifiedPerson", c.QualifiedPerson);
            w.WriteString("account", c.QualifiedPersonAccount);
            w.WriteString("meaning", c.Meaning);
            if (c.Comment is null)
                w.WriteNull("comment");
            else
                w.WriteString("comment", c.Comment);
            w.WriteString("signedAt", Utc(c.SignedUtc));
            w.WriteString("keyProvider", c.KeyProvider);
            w.WriteBoolean("hardwareKey", c.HardwareKey);
            w.WriteEndObject();
            w.WriteStartArray("documents");
            foreach (var d in c.Documents.OrderBy(d => d.Kind, StringComparer.Ordinal).ThenBy(d => d.FileName, StringComparer.Ordinal))
            {
                w.WriteStartObject();
                w.WriteString("kind", d.Kind);
                w.WriteString("file", d.FileName);
                w.WriteString("sha256", d.Sha256.ToLowerInvariant());
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteStartArray("deviations");
            foreach (var number in c.Deviations.OrderBy(n => n, StringComparer.Ordinal))
                w.WriteStringValue(number);
            w.WriteEndArray();
            w.WriteEndObject();
        }
        return buffer.ToArray();
    }

    public static string Sha256Hex(ReadOnlySpan<byte> data) => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

    public static ReleaseCertificateSummary ReadSummary(ReadOnlySpan<byte> json)
    {
        using var doc = JsonDocument.Parse(json.ToArray());
        var root = doc.RootElement;
        var batch = root.GetProperty("batch");
        var signature = root.GetProperty("signature");
        return new ReleaseCertificateSummary(
            root.GetProperty("certificateNumber").GetString()!,
            root.GetProperty("decision").GetString()!,
            batch.GetProperty("number").GetString()!,
            batch.GetProperty("productCode").GetString()!,
            DateOnly.ParseExact(batch.GetProperty("expiry").GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture),
            signature.GetProperty("qualifiedPerson").GetString()!,
            DateTime.Parse(signature.GetProperty("signedAt").GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal));
    }

    private static string Utc(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
}

public sealed record ReleaseCertificateSummary(
    string CertificateNumber, string Decision, string BatchNumber, string ProductCode,
    DateOnly ExpiresOn, string QualifiedPerson, DateTime SignedUtc);
