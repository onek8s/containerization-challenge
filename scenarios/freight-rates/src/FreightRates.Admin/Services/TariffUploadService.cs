using Microsoft.Extensions.Options;

namespace FreightRates.Admin.Services;

/// <summary>
/// Copies a tariff release to the rating host incoming share. The rating host installs the
/// files and reloads the engine when the release.ok marker is written.
/// </summary>
public sealed class TariffUploadService
{
    public static readonly string[] AllowedExtensions = { ".mrt", ".mrx", ".mrz" };

    private readonly AdminOptions _options;
    private readonly ILogger<TariffUploadService> _logger;

    public TariffUploadService(IOptions<AdminOptions> options, ILogger<TariffUploadService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<int> UploadAsync(IReadOnlyList<IFormFile> files, string uploadedBy, CancellationToken cancellationToken)
    {
        var invalid = files.Where(f => !AllowedExtensions.Contains(Path.GetExtension(f.FileName), StringComparer.OrdinalIgnoreCase)).ToList();
        if (invalid.Count > 0)
            throw new InvalidOperationException($"Not a tariff file: {string.Join(", ", invalid.Select(f => f.FileName))}");

        var share = _options.TariffIncomingShare;
        if (!Directory.Exists(share))
            throw new DirectoryNotFoundException($"Tariff share {share} is not reachable.");

        foreach (var file in files)
        {
            var target = Path.Combine(share, Path.GetFileName(file.FileName));
            await using var stream = File.Create(target);
            await file.CopyToAsync(stream, cancellationToken);
        }

        await File.WriteAllTextAsync(
            Path.Combine(share, "release.ok"),
            $"{DateTime.Now:s} {uploadedBy} {files.Count}{Environment.NewLine}",
            cancellationToken);

        _logger.LogInformation("{User} released {Count} tariff files to {Share}", uploadedBy, files.Count, share);
        return files.Count;
    }
}
