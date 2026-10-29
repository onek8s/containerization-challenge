using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CityPermits.Archive;

public sealed record ArchivedDocument(Guid Key, string Path, long Length, string Sha256);

public sealed record ArchiveSearchHit(Guid Key, string Name, string Path, int Rank);

/// <summary>Case document store. Documents are filed under a folder per case reference.</summary>
public interface IDocumentArchive
{
    Task<ArchivedDocument> StoreAsync(string caseReference, string fileName, Stream content, CancellationToken ct = default);
    Task<Stream> OpenReadAsync(Guid key, CancellationToken ct = default);
    Task<IReadOnlyList<ArchiveSearchHit>> SearchAsync(string text, int max = 50, CancellationToken ct = default);
    /// <summary>Path that other systems (records system, scanning) use to reach the file.</summary>
    Task<string?> SharePathAsync(Guid key, CancellationToken ct = default);
}

public sealed class ArchiveOptions
{
    public const string Section = "Archive";
    /// <summary>FileTable (production) or Folder (development).</summary>
    public string Provider { get; set; } = "FileTable";
    public string? ConnectionString { get; set; }
    public string FileTable { get; set; } = "permits.CaseDocuments";
    public string FolderPath { get; set; } = "App_Data/archive";
}

public static class ArchiveServiceCollectionExtensions
{
    public static IServiceCollection AddDocumentArchive(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ArchiveOptions>(configuration.GetSection(ArchiveOptions.Section));
        services.PostConfigure<ArchiveOptions>(o => o.ConnectionString ??= configuration.GetConnectionString("Archive") ?? configuration.GetConnectionString("Permits"));
        if ((configuration["Archive:Provider"] ?? "FileTable").Equals("Folder", StringComparison.OrdinalIgnoreCase))
            services.AddSingleton<IDocumentArchive, FolderArchive>();
        else
            services.AddSingleton<IDocumentArchive, FileTableArchive>();
        return services;
    }

    internal static string FolderName(string caseReference) => caseReference.Replace('/', '-');

    internal static async Task<(long Length, string Sha)> CopyWithHashAsync(Stream source, Stream target, CancellationToken ct)
    {
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, ct)) > 0)
        {
            sha.AppendData(buffer, 0, read);
            await target.WriteAsync(buffer.AsMemory(0, read), ct);
            total += read;
        }
        return (total, Convert.ToHexString(sha.GetHashAndReset()).ToLowerInvariant());
    }
}
