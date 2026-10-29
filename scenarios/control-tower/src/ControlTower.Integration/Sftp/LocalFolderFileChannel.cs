namespace Proseware.ControlTower.Integration.Sftp;

/// <summary>Partner exchange on a local folder with the same sub folders as the SFTP account.</summary>
public sealed class LocalFolderFileChannel : IPartnerFileChannel
{
    private readonly SftpOptions _options;
    private readonly string _root;

    public LocalFolderFileChannel(SftpOptions options)
    {
        _options = options;
        _root = options.LocalFolder!;
        Directory.CreateDirectory(Map(options.InboundDirectory));
        Directory.CreateDirectory(Map(options.ArchiveDirectory));
        Directory.CreateDirectory(Map(options.OutboundDirectory));
    }

    public IReadOnlyList<RemoteFile> ListInbound()
    {
        var pattern = SshNetFileChannel.WildcardToRegex(_options.FilePattern);
        return new DirectoryInfo(Map(_options.InboundDirectory)).EnumerateFiles()
            .Where(f => pattern.IsMatch(f.Name))
            .OrderBy(f => f.LastWriteTimeUtc)
            .Select(f => new RemoteFile(f.Name, f.FullName, f.Length, f.LastWriteTimeUtc))
            .ToList();
    }

    public byte[] Download(RemoteFile file) => File.ReadAllBytes(file.FullPath);

    public void Archive(RemoteFile file) =>
        File.Move(file.FullPath, Path.Combine(Map(_options.ArchiveDirectory), file.Name), overwrite: true);

    public void Upload(string fileName, byte[] content)
    {
        var dir = Map(_options.OutboundDirectory);
        var tmp = Path.Combine(dir, fileName + ".tmp");
        File.WriteAllBytes(tmp, content);
        File.Move(tmp, Path.Combine(dir, fileName), overwrite: true);
    }

    public void Dispose()
    {
    }

    private string Map(string remote) => Path.Combine(_root, remote.Trim('/').Replace('/', Path.DirectorySeparatorChar));
}
