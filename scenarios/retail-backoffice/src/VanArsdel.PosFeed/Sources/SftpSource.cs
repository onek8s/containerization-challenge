using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Renci.SshNet;
using Renci.SshNet.Sftp;

namespace VanArsdel.PosFeed.Sources
{
    /// <summary>The new POS vendor (2020) pushes the exports to their SFTP server; we pull from there.</summary>
    public class SftpSource : IPosFileSource, IDisposable
    {
        private readonly SftpSourceOptions _options;
        private readonly PosFeedOptions _feed;
        private readonly ILogger<SftpSource> _logger;
        private SftpClient _client;

        public SftpSource(IOptions<PosFeedOptions> options, ILogger<SftpSource> logger)
        {
            _feed = options.Value;
            _options = options.Value.Sftp;
            _logger = logger;
        }

        public string Description => $"sftp://{_options.Username}@{_options.Host}:{_options.Port}{_options.RemoteFolder}";

        public Task<IReadOnlyList<PosFile>> ListAsync(CancellationToken cancellationToken)
        {
            var client = Connect();
            var cutoff = DateTime.UtcNow.AddSeconds(-_feed.MinFileAgeSeconds);
            var pattern = _feed.FilePattern.Replace("*", string.Empty).Split('.');
            IReadOnlyList<PosFile> files = client.ListDirectory(_options.RemoteFolder)
                .Where(f => f.IsRegularFile && f.LastWriteTimeUtc < cutoff
                            && f.Name.StartsWith(pattern[0], StringComparison.OrdinalIgnoreCase)
                            && f.Name.EndsWith("." + pattern.Last(), StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                .Select(f => new PosFile { Name = f.Name, Size = f.Length, Handle = f.FullName })
                .ToList();
            return Task.FromResult(files);
        }

        public Task<Stream> OpenAsync(PosFile file, CancellationToken cancellationToken)
        {
            var buffer = new MemoryStream();
            Connect().DownloadFile((string)file.Handle, buffer);
            buffer.Position = 0;
            return Task.FromResult<Stream>(buffer);
        }

        public Task CompleteAsync(PosFile file, CancellationToken cancellationToken)
        {
            Move(file, _options.RemoteArchiveFolder);
            return Task.CompletedTask;
        }

        public Task FailAsync(PosFile file, string reason, CancellationToken cancellationToken)
        {
            var client = Move(file, _options.RemoteErrorFolder);
            using (var note = new MemoryStream(Encoding.UTF8.GetBytes(reason)))
            {
                client.UploadFile(note, _options.RemoteErrorFolder.TrimEnd('/') + "/" + file.Name + ".err", true);
            }
            return Task.CompletedTask;
        }

        private SftpClient Move(PosFile file, string folder)
        {
            var client = Connect();
            var target = folder.TrimEnd('/') + "/" + file.Name;
            if (client.Exists(target)) client.DeleteFile(target);
            client.RenameFile((string)file.Handle, target);
            return client;
        }

        private SftpClient Connect()
        {
            if (_client != null && _client.IsConnected) return _client;
            _client?.Dispose();

            var methods = new List<AuthenticationMethod>();
            if (!string.IsNullOrEmpty(_options.PrivateKeyPath))
            {
                var key = string.IsNullOrEmpty(_options.PrivateKeyPassphrase)
                    ? new PrivateKeyFile(_options.PrivateKeyPath)
                    : new PrivateKeyFile(_options.PrivateKeyPath, _options.PrivateKeyPassphrase);
                methods.Add(new PrivateKeyAuthenticationMethod(_options.Username, key));
            }
            if (!string.IsNullOrEmpty(_options.Password))
            {
                methods.Add(new PasswordAuthenticationMethod(_options.Username, _options.Password));
            }

            var connection = new ConnectionInfo(_options.Host, _options.Port, _options.Username, methods.ToArray())
            {
                Timeout = TimeSpan.FromSeconds(30),
            };
            _client = new SftpClient(connection);
            _client.HostKeyReceived += (sender, e) =>
            {
                var fingerprint = BitConverter.ToString(e.FingerPrint).Replace("-", ":").ToLowerInvariant();
                e.CanTrust = string.IsNullOrEmpty(_options.HostKeyFingerprint)
                             || string.Equals(fingerprint, _options.HostKeyFingerprint, StringComparison.OrdinalIgnoreCase);
                if (!e.CanTrust) _logger.LogError("SFTP host key {Fingerprint} does not match the configured key", fingerprint);
            };
            _client.Connect();
            _logger.LogInformation("Connected to {Source}", Description);
            return _client;
        }

        public void Dispose() => _client?.Dispose();
    }
}
