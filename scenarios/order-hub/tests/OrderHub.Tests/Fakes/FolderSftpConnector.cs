using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Litware.OrderHub.Integration.Sftp;

namespace Litware.OrderHub.Tests.Fakes
{
    /// <summary>SFTP stand-in that maps the remote root to a local folder.</summary>
    internal class FolderSftpConnector : ISftpConnector
    {
        public FolderSftpConnector(string root)
        {
            Root = root;
            Directory.CreateDirectory(root);
        }

        public string Root { get; }

        public string Local(string remote) => System.IO.Path.Combine(Root, remote.TrimStart('/').Replace('/', System.IO.Path.DirectorySeparatorChar));

        public void Put(string remote, byte[] content)
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Local(remote)));
            File.WriteAllBytes(Local(remote), content);
        }

        public ISftpSession Connect() => new Session(this);

        private class Session : ISftpSession
        {
            private readonly FolderSftpConnector _c;
            public Session(FolderSftpConnector c) { _c = c; }

            public IList<string> ListFiles(string directory, string extension)
            {
                string local = _c.Local(directory);
                if (!Directory.Exists(local)) return new List<string>();
                return Directory.GetFiles(local, "*" + extension).OrderBy(f => f)
                    .Select(f => directory.TrimEnd('/') + "/" + System.IO.Path.GetFileName(f)).ToList();
            }

            public byte[] Download(string path) => File.ReadAllBytes(_c.Local(path));

            public void Upload(string directory, string fileName, byte[] content) => _c.Put(directory.TrimEnd('/') + "/" + fileName, content);

            public void Move(string path, string targetDirectory)
            {
                Directory.CreateDirectory(_c.Local(targetDirectory));
                File.Move(_c.Local(path), System.IO.Path.Combine(_c.Local(targetDirectory), System.IO.Path.GetFileName(_c.Local(path))), true);
            }

            public void Dispose() { }
        }
    }
}
