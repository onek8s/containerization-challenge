using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using log4net;
using Renci.SshNet;
using Renci.SshNet.Common;
using Tailspin.DealerOrders.Domain;

namespace Tailspin.DealerOrders.EdiGateway.Sftp
{
    /// <summary>
    /// SFTP with our key pair; the dealer's host key must be listed in the known hosts file ("host md5-fingerprint" per line).
    /// Outbound files are uploaded under a temporary name and renamed, so the dealer never picks up half a file.
    /// </summary>
    public class SftpTransport : IFileTransport
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(SftpTransport));

        private readonly GatewaySettings _settings;

        public SftpTransport(GatewaySettings settings)
        {
            _settings = settings;
        }

        public IFileSession Open(EdiPartner partner)
        {
            var key = string.IsNullOrEmpty(_settings.SftpKeyPassphrase)
                ? new PrivateKeyFile(_settings.SftpKeyFile)
                : new PrivateKeyFile(_settings.SftpKeyFile, _settings.SftpKeyPassphrase);
            var connection = new ConnectionInfo(partner.SftpHost, partner.SftpPort == 0 ? 22 : partner.SftpPort, partner.SftpUser,
                new PrivateKeyAuthenticationMethod(partner.SftpUser, key)) { Timeout = TimeSpan.FromSeconds(30) };
            var client = new SftpClient(connection);
            var known = KnownHost(partner.SftpHost);
            client.HostKeyReceived += (sender, e) =>
            {
                var fingerprint = string.Join(":", e.FingerPrint.Select(b => b.ToString("x2")));
                e.CanTrust = known != null && string.Equals(known, fingerprint, StringComparison.OrdinalIgnoreCase);
                if (!e.CanTrust) Log.ErrorFormat("Host key {0} of {1} is not trusted (known hosts has {2})", fingerprint, partner.SftpHost, known ?? "nothing");
            };
            client.Connect();
            return new Session(client, partner, _settings.SftpArchiveFolder);
        }

        private string KnownHost(string host)
        {
            if (!File.Exists(_settings.SftpKnownHostsFile)) return null;
            return File.ReadAllLines(_settings.SftpKnownHostsFile)
                .Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith("#"))
                .Select(l => l.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
                .Where(p => p.Length >= 2 && string.Equals(p[0], host, StringComparison.OrdinalIgnoreCase))
                .Select(p => p[1]).FirstOrDefault();
        }

        private class Session : IFileSession
        {
            private readonly SftpClient _client;
            private readonly EdiPartner _partner;
            private readonly string _archive;

            public Session(SftpClient client, EdiPartner partner, string archive)
            {
                _client = client;
                _partner = partner;
                _archive = archive;
            }

            public IList<string> ListInbound() =>
                _client.ListDirectory(_partner.SftpInboundFolder)
                    .Where(f => f.IsRegularFile && !f.Name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(f => f.LastWriteTimeUtc).Select(f => f.Name).ToList();

            public string Read(string name)
            {
                using (var stream = new MemoryStream())
                {
                    _client.DownloadFile(_partner.SftpInboundFolder.TrimEnd('/') + "/" + name, stream);
                    return Encoding.ASCII.GetString(stream.ToArray());
                }
            }

            public void Archive(string name)
            {
                var from = _partner.SftpInboundFolder.TrimEnd('/') + "/" + name;
                var to = _partner.SftpInboundFolder.TrimEnd('/') + "/" + _archive + "/" + name;
                try
                {
                    _client.RenameFile(from, to);
                }
                catch (SftpPathNotFoundException)
                {
                    // Some dealers give us no archive folder; delete instead (the content is in edi.Message).
                    _client.DeleteFile(from);
                }
            }

            public void Write(string name, string content)
            {
                var folder = _partner.SftpOutboundFolder.TrimEnd('/');
                using (var stream = new MemoryStream(Encoding.ASCII.GetBytes(content)))
                    _client.UploadFile(stream, folder + "/" + name + ".tmp");
                _client.RenameFile(folder + "/" + name + ".tmp", folder + "/" + name);
            }

            public void Dispose()
            {
                if (_client.IsConnected) _client.Disconnect();
                _client.Dispose();
            }
        }
    }

    /// <summary>Partner folders under a local root (test systems without the dealers' SFTP servers).</summary>
    public class LocalFolderTransport : IFileTransport
    {
        private readonly GatewaySettings _settings;

        public LocalFolderTransport(GatewaySettings settings)
        {
            _settings = settings;
        }

        public IFileSession Open(EdiPartner partner)
        {
            var root = Path.Combine(_settings.SftpLocalRoot, partner.SftpUser ?? partner.Id.ToString());
            return new Session(Folder(root, partner.SftpInboundFolder), Folder(root, partner.SftpOutboundFolder), _settings.SftpArchiveFolder);
        }

        private static string Folder(string root, string relative)
        {
            var path = Path.Combine(root, (relative ?? "").Trim('/').Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(path);
            return path;
        }

        private class Session : IFileSession
        {
            private readonly string _inbound;
            private readonly string _outbound;
            private readonly string _archive;

            public Session(string inbound, string outbound, string archive)
            {
                _inbound = inbound;
                _outbound = outbound;
                _archive = Path.Combine(inbound, archive);
            }

            public IList<string> ListInbound() =>
                new DirectoryInfo(_inbound).GetFiles().Where(f => !f.Name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(f => f.LastWriteTimeUtc).Select(f => f.Name).ToList();

            public string Read(string name) => File.ReadAllText(Path.Combine(_inbound, name), Encoding.ASCII);

            public void Archive(string name)
            {
                Directory.CreateDirectory(_archive);
                var target = Path.Combine(_archive, name);
                if (File.Exists(target)) File.Delete(target);
                File.Move(Path.Combine(_inbound, name), target);
            }

            public void Write(string name, string content)
            {
                var temp = Path.Combine(_outbound, name + ".tmp");
                File.WriteAllText(temp, content, Encoding.ASCII);
                File.Move(temp, Path.Combine(_outbound, name));
            }

            public void Dispose() { }
        }
    }
}
