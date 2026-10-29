using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Renci.SshNet;
using Renci.SshNet.Common;
using Serilog;

namespace Litware.OrderHub.Integration.Sftp
{
    public class SshNetSftpConnector : ISftpConnector
    {
        private readonly SftpSettings _settings;

        public SshNetSftpConnector(SftpSettings settings)
        {
            _settings = settings;
        }

        public ISftpSession Connect()
        {
            var methods = new List<AuthenticationMethod>();
            if (!string.IsNullOrEmpty(_settings.PrivateKeyFile))
            {
                var key = string.IsNullOrEmpty(_settings.PrivateKeyPassphrase)
                    ? new PrivateKeyFile(_settings.PrivateKeyFile)
                    : new PrivateKeyFile(_settings.PrivateKeyFile, _settings.PrivateKeyPassphrase);
                methods.Add(new PrivateKeyAuthenticationMethod(_settings.Username, key));
            }
            if (!string.IsNullOrEmpty(_settings.Password))
            {
                methods.Add(new PasswordAuthenticationMethod(_settings.Username, _settings.Password));
            }
            if (methods.Count == 0)
            {
                throw new InvalidOperationException("No password or private key configured for " + _settings + ".");
            }

            var info = new ConnectionInfo(_settings.Host, _settings.Port, _settings.Username, methods.ToArray())
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            var client = new SftpClient(info);
            client.HostKeyReceived += OnHostKeyReceived;
            client.Connect();
            return new Session(client);
        }

        private void OnHostKeyReceived(object sender, HostKeyEventArgs e)
        {
            string actual = string.Join(":", e.FingerPrint.Select(b => b.ToString("x2")));
            if (string.IsNullOrEmpty(_settings.HostKeyFingerprint))
            {
                Log.Warning("No host key fingerprint configured for {Server}; server presented {Fingerprint}", _settings.ToString(), actual);
                e.CanTrust = true;
                return;
            }
            e.CanTrust = string.Equals(actual, _settings.HostKeyFingerprint.Trim(), StringComparison.OrdinalIgnoreCase);
            if (!e.CanTrust)
            {
                Log.Error("Host key of {Server} is {Fingerprint}, expected {Expected}", _settings.ToString(), actual, _settings.HostKeyFingerprint);
            }
        }

        private class Session : ISftpSession
        {
            private readonly SftpClient _client;

            public Session(SftpClient client)
            {
                _client = client;
            }

            public IList<string> ListFiles(string directory, string extension)
            {
                return _client.ListDirectory(directory)
                    .Where(f => f.IsRegularFile && f.Name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(f => f.LastWriteTimeUtc)
                    .Select(f => f.FullName)
                    .ToList();
            }

            public byte[] Download(string path)
            {
                using (var stream = new MemoryStream())
                {
                    _client.DownloadFile(path, stream);
                    return stream.ToArray();
                }
            }

            public void Upload(string directory, string fileName, byte[] content)
            {
                string target = Combine(directory, fileName);
                string temp = Combine(directory, "." + fileName + ".part");
                using (var stream = new MemoryStream(content))
                {
                    _client.UploadFile(stream, temp, true);
                }
                if (_client.Exists(target))
                {
                    _client.DeleteFile(target);
                }
                _client.RenameFile(temp, target);
            }

            public void Move(string path, string targetDirectory)
            {
                if (!_client.Exists(targetDirectory))
                {
                    _client.CreateDirectory(targetDirectory);
                }
                string name = path.Substring(path.LastIndexOf('/') + 1);
                string target = Combine(targetDirectory, name);
                if (_client.Exists(target))
                {
                    target = Combine(targetDirectory, DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "_" + name);
                }
                _client.RenameFile(path, target);
            }

            private static string Combine(string directory, string name)
            {
                return directory.TrimEnd('/') + "/" + name;
            }

            public void Dispose()
            {
                if (_client.IsConnected) _client.Disconnect();
                _client.Dispose();
            }
        }
    }
}
