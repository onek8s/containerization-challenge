using Microsoft.Extensions.Options;

namespace StaffOnboarding.Portal.Services;

/// <summary>Employment contracts and other request documents on the onboarding share.</summary>
public sealed class DocumentStore
{
    private readonly PortalOptions _options;

    public DocumentStore(IOptions<PortalOptions> options)
    {
        _options = options.Value;
    }

    public IReadOnlyList<FileInfo> List(string requestNumber)
    {
        var folder = new DirectoryInfo(Folder(requestNumber));
        return folder.Exists ? folder.GetFiles().OrderBy(f => f.Name).ToList() : Array.Empty<FileInfo>();
    }

    public async Task<string> SaveAsync(string requestNumber, IFormFile file)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!_options.AllowedDocumentExtensions.Contains(extension))
            throw new InvalidOperationException($"Files of type {extension} cannot be attached.");
        if (file.Length > _options.MaxDocumentBytes)
            throw new InvalidOperationException("The file is too large.");

        var folder = Directory.CreateDirectory(Folder(requestNumber));
        var name = SafeName(Path.GetFileNameWithoutExtension(file.FileName)) + extension;
        var path = Path.Combine(folder.FullName, name);
        for (var i = 2; File.Exists(path); i++)
            path = Path.Combine(folder.FullName, $"{SafeName(Path.GetFileNameWithoutExtension(file.FileName))} ({i}){extension}");

        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await file.CopyToAsync(stream);
        return Path.GetFileName(path);
    }

    public FileInfo? Find(string requestNumber, string fileName)
    {
        if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || fileName.Contains(".."))
            return null;
        var file = new FileInfo(Path.Combine(Folder(requestNumber), fileName));
        return file.Exists ? file : null;
    }

    private string Folder(string requestNumber) =>
        Path.Combine(_options.DocumentsShare, requestNumber.Substring(4, 4), requestNumber);

    private static string SafeName(string name)
    {
        var chars = name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray();
        var s = new string(chars).Trim();
        return s.Length == 0 ? "document" : s.Length > 80 ? s[..80] : s;
    }
}
