using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using log4net;
using Renci.SshNet;
using Renci.SshNet.Sftp;

namespace Southridge.Clinic.BillingBatch
{
    /// <summary>How claim files reach the clearinghouse and how 999/835 files come back.</summary>
    public interface IClearinghouseTransport : IDisposable
    {
        void Upload(string localFile);
        IList<string> ListInbound();
        void Download(string name, string localFile);
        void RemoveInbound(string name);
    }

    public static class ClearinghouseTransport
    {
        public static IClearinghouseTransport Create(BatchSettings settings)
        {
            switch ((settings.Transport ?? string.Empty).ToUpperInvariant())
            {
                case "SFTP":
                    return new SftpTransport(settings);
                case "FOLDER":
                    return new FolderTransport(settings.ExchangeOutbound, settings.ExchangeInbound);
                default:
                    throw new InvalidOperationException("Billing:Transport must be Sftp or Folder.");
            }
        }
    }

    /// <summary>Drop folders (clearinghouse agent on the same network, or the test environment).</summary>
    public class FolderTransport : IClearinghouseTransport
    {
        private readonly string _outbound;
        private readonly string _inbound;

        public FolderTransport(string outbound, string inbound)
        {
            _outbound = outbound;
            _inbound = inbound;
            Directory.CreateDirectory(_outbound);
            Directory.CreateDirectory(_inbound);
        }

        public void Upload(string localFile)
        {
            string target = Path.Combine(_outbound, Path.GetFileName(localFile));
            string temp = target + ".part";
            File.Copy(localFile, temp, true);
            if (File.Exists(target))
            {
                File.Delete(target);
            }

            File.Move(temp, target);
        }

        public IList<string> ListInbound()
        {
            return Directory.GetFiles(_inbound).Select(Path.GetFileName).Where(n => !n.EndsWith(".part", StringComparison.OrdinalIgnoreCase)).OrderBy(n => n).ToList();
        }

        public void Download(string name, string localFile)
        {
            File.Copy(Path.Combine(_inbound, name), localFile, true);
        }

        public void RemoveInbound(string name)
        {
            File.Delete(Path.Combine(_inbound, name));
        }

        public void Dispose()
        {
        }
    }

    /// <summary>Clearinghouse SFTP (SSH.NET): claims to /inbound, responses from /outbound.</summary>
    public class SftpTransport : IClearinghouseTransport
    {
        private const string UploadPath = "/inbound";
        private const string DownloadPath = "/outbound";
        private static readonly ILog Log = LogManager.GetLogger(typeof(SftpTransport));

        private readonly SftpClient _client;

        public SftpTransport(BatchSettings settings)
        {
            ConnectionInfo connection = string.IsNullOrEmpty(settings.SftpKeyFile)
                ? new ConnectionInfo(settings.SftpHost, settings.SftpPort, settings.SftpUser, new PasswordAuthenticationMethod(settings.SftpUser, settings.SftpPassword))
                : new ConnectionInfo(settings.SftpHost, settings.SftpPort, settings.SftpUser, new PrivateKeyAuthenticationMethod(settings.SftpUser, new PrivateKeyFile(settings.SftpKeyFile)));
            connection.Timeout = TimeSpan.FromSeconds(60);
            _client = new SftpClient(connection);
            _client.Connect();
            Log.InfoFormat("Connected to {0}:{1} as {2}", settings.SftpHost, settings.SftpPort, settings.SftpUser);
        }

        public void Upload(string localFile)
        {
            string name = Path.GetFileName(localFile);
            using (FileStream stream = File.OpenRead(localFile))
            {
                _client.UploadFile(stream, UploadPath + "/" + name + ".part");
            }

            _client.RenameFile(UploadPath + "/" + name + ".part", UploadPath + "/" + name);
        }

        public IList<string> ListInbound()
        {
            return _client.ListDirectory(DownloadPath).Where(f => f.IsRegularFile).Select(f => f.Name).OrderBy(n => n).ToList();
        }

        public void Download(string name, string localFile)
        {
            using (FileStream stream = File.Create(localFile))
            {
                _client.DownloadFile(DownloadPath + "/" + name, stream);
            }
        }

        public void RemoveInbound(string name)
        {
            _client.DeleteFile(DownloadPath + "/" + name);
        }

        public void Dispose()
        {
            if (_client.IsConnected)
            {
                _client.Disconnect();
            }

            _client.Dispose();
        }
    }
}
