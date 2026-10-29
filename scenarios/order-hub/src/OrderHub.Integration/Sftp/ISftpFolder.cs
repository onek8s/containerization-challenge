using System.Collections.Generic;

namespace Litware.OrderHub.Integration.Sftp
{
    /// <summary>The few SFTP operations the hub needs; one connection per call batch.</summary>
    public interface ISftpSession : System.IDisposable
    {
        IList<string> ListFiles(string directory, string extension);
        byte[] Download(string path);

        /// <summary>Uploads under a temporary name and renames, so the other side never reads a partial file.</summary>
        void Upload(string directory, string fileName, byte[] content);

        void Move(string path, string targetDirectory);
    }

    public interface ISftpConnector
    {
        ISftpSession Connect();
    }
}
