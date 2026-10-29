using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Renci.SshNet;

namespace Fabrikam.Housing.Integration.Bacs
{
    /// <summary>
    /// Uploads Standard 18 files to the payment bureau over SFTP with the Fabrikam key pair.
    /// The file is written as .tmp and renamed so the bureau never picks up a partial file.
    /// </summary>
    public class SftpBacsBureau : IBacsBureau
    {
        private readonly BacsBureauOptions _options;
        private readonly ILogger<SftpBacsBureau> _logger;

        public SftpBacsBureau(IOptions<IntegrationOptions> options, ILogger<SftpBacsBureau> logger)
        {
            _options = options.Value.BacsBureau;
            _logger = logger;
        }

        public Task UploadAsync(string fileName, string content)
        {
            return Task.Run(() =>
            {
                var key = string.IsNullOrEmpty(_options.PrivateKeyPassphrase)
                    ? new PrivateKeyFile(_options.PrivateKeyFile)
                    : new PrivateKeyFile(_options.PrivateKeyFile, _options.PrivateKeyPassphrase);
                using (var client = new SftpClient(_options.Host, _options.Port, _options.UserName, key))
                {
                    client.HostKeyReceived += (sender, e) =>
                    {
                        var fingerprint = string.Join(":", e.FingerPrint.Select(b => b.ToString("x2")));
                        e.CanTrust = string.Equals(fingerprint, _options.HostKeyFingerprint, StringComparison.OrdinalIgnoreCase);
                        if (!e.CanTrust) _logger.LogError("Bacs bureau host key {Fingerprint} does not match the configured fingerprint", fingerprint);
                    };
                    client.Connect();
                    var remote = _options.RemoteFolder.TrimEnd('/') + "/" + fileName;
                    using (var stream = new MemoryStream(Encoding.ASCII.GetBytes(content)))
                        client.UploadFile(stream, remote + ".tmp", true);
                    client.RenameFile(remote + ".tmp", remote);
                    client.Disconnect();
                    _logger.LogInformation("Uploaded {File} to {Host}{Remote}", fileName, _options.Host, remote);
                }
            });
        }
    }
}
