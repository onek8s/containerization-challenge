namespace Proseware.ControlTower.Integration.Sftp;

public sealed record RemoteFile(string Name, string FullPath, long Length, DateTime LastWriteUtc);

/// <summary>File exchange with a partner: an SFTP server or, in test setups, a local folder.</summary>
public interface IPartnerFileChannel : IDisposable
{
    IReadOnlyList<RemoteFile> ListInbound();

    byte[] Download(RemoteFile file);

    /// <summary>Moves a processed inbound file to the archive directory.</summary>
    void Archive(RemoteFile file);

    /// <summary>Uploads as "&lt;name&gt;.tmp" and renames, so the partner never picks up a partial file.</summary>
    void Upload(string fileName, byte[] content);
}

public interface IPartnerFileChannelFactory
{
    IPartnerFileChannel Open(SftpOptions options);
}

public sealed class PartnerFileChannelFactory : IPartnerFileChannelFactory
{
    public IPartnerFileChannel Open(SftpOptions options) =>
        string.IsNullOrEmpty(options.LocalFolder)
            ? new SshNetFileChannel(options)
            : new LocalFolderFileChannel(options);
}
