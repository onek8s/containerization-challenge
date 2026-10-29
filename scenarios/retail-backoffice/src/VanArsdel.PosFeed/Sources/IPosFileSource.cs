using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace VanArsdel.PosFeed.Sources
{
    public class PosFile
    {
        public string Name { get; set; }
        public long Size { get; set; }
        public object Handle { get; set; }
    }

    /// <summary>Where the tills drop their exports. Folder share until 2020, SFTP after the POS vendor change.</summary>
    public interface IPosFileSource
    {
        string Description { get; }
        Task<IReadOnlyList<PosFile>> ListAsync(CancellationToken cancellationToken);
        Task<Stream> OpenAsync(PosFile file, CancellationToken cancellationToken);
        Task CompleteAsync(PosFile file, CancellationToken cancellationToken);
        Task FailAsync(PosFile file, string reason, CancellationToken cancellationToken);
    }

    public class PosFeedOptions
    {
        public string Source { get; set; } = "Folder";
        public int PollSeconds { get; set; } = 30;
        public string FilePattern { get; set; } = "POS_*.txt";

        /// <summary>Files younger than this are still being written by the store router.</summary>
        public int MinFileAgeSeconds { get; set; } = 10;

        public FolderSourceOptions Folder { get; set; } = new FolderSourceOptions();
        public SftpSourceOptions Sftp { get; set; } = new SftpSourceOptions();
    }

    public class FolderSourceOptions
    {
        public string Inbox { get; set; }
        public string Archive { get; set; }
        public string Error { get; set; }
    }

    public class SftpSourceOptions
    {
        public string Host { get; set; }
        public int Port { get; set; } = 22;
        public string Username { get; set; }
        public string Password { get; set; }
        public string PrivateKeyPath { get; set; }
        public string PrivateKeyPassphrase { get; set; }
        public string RemoteFolder { get; set; } = "/outbound";
        public string RemoteArchiveFolder { get; set; } = "/outbound/processed";
        public string RemoteErrorFolder { get; set; } = "/outbound/error";
        public string HostKeyFingerprint { get; set; }
    }
}
