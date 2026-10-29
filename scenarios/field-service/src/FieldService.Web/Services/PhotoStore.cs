using Microsoft.Extensions.Options;

namespace FieldService.Web.Services;

/// <summary>
/// Stores job photos under {PhotoRoot}\{work order number}\.
/// </summary>
public class PhotoStore
{
    private static readonly Dictionary<string, string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".heic"] = "image/heic",
        [".pdf"] = "application/pdf"
    };

    private readonly PortalOptions _options;

    public PhotoStore(IOptions<PortalOptions> options)
    {
        _options = options.Value;
    }

    public static bool IsAllowed(string fileName) => AllowedTypes.ContainsKey(Path.GetExtension(fileName));

    public static string ContentTypeFor(string fileName) =>
        AllowedTypes.TryGetValue(Path.GetExtension(fileName), out var type) ? type : "application/octet-stream";

    public async Task<string> SaveAsync(string workOrderNumber, IFormFile file, CancellationToken ct)
    {
        var folder = Path.Combine(_options.PhotoRoot, workOrderNumber);
        Directory.CreateDirectory(folder);
        var storedName = $"{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}{Path.GetExtension(file.FileName).ToLowerInvariant()}";
        await using var target = File.Create(Path.Combine(folder, storedName));
        await file.CopyToAsync(target, ct);
        return storedName;
    }

    public FileStream? Open(string workOrderNumber, string storedName)
    {
        var path = Path.Combine(_options.PhotoRoot, workOrderNumber, Path.GetFileName(storedName));
        return File.Exists(path) ? File.OpenRead(path) : null;
    }
}
