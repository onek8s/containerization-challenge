using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Renci.SshNet;
using Renci.SshNet.Common;
using Renci.SshNet.Sftp;

namespace Proseware.ControlTower.Integration.Sftp;

public sealed class SshNetFileChannel : IPartnerFileChannel
{
    private readonly SftpOptions _options;
    private readonly SftpClient _client;
    private readonly Regex _pattern;

    public SshNetFileChannel(SftpOptions options)
    {
        _options = options;
        _pattern = WildcardToRegex(options.FilePattern);
        var methods = new List<AuthenticationMethod>();
        if (!string.IsNullOrEmpty(options.PrivateKeyFile))
        {
            var key = string.IsNullOrEmpty(options.PrivateKeyPassphrase)
                ? new PrivateKeyFile(options.PrivateKeyFile)
                : new PrivateKeyFile(options.PrivateKeyFile, options.PrivateKeyPassphrase);
            methods.Add(new PrivateKeyAuthenticationMethod(options.UserName, key));
        }
        if (!string.IsNullOrEmpty(options.Password))
            methods.Add(new PasswordAuthenticationMethod(options.UserName, options.Password));
        if (methods.Count == 0) throw new InvalidOperationException($"No password or private key configured for {options.UserName}@{options.Host}.");

        var connection = new ConnectionInfo(options.Host, options.Port, options.UserName, methods.ToArray())
        {
            Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds)
        };
        _client = new SftpClient(connection);
        _client.HostKeyReceived += OnHostKeyReceived;
        _client.Connect();
    }

    public IReadOnlyList<RemoteFile> ListInbound() =>
        _client.ListDirectory(_options.InboundDirectory)
            .Where(f => f.IsRegularFile && _pattern.IsMatch(f.Name))
            .OrderBy(f => f.LastWriteTimeUtc)
            .Select(f => new RemoteFile(f.Name, f.FullName, f.Length, f.LastWriteTimeUtc))
            .ToList();

    public byte[] Download(RemoteFile file)
    {
        using var ms = new MemoryStream();
        _client.DownloadFile(file.FullPath, ms);
        return ms.ToArray();
    }

    public void Archive(RemoteFile file)
    {
        var target = $"{_options.ArchiveDirectory.TrimEnd('/')}/{file.Name}";
        if (_client.Exists(target)) _client.DeleteFile(target);
        _client.RenameFile(file.FullPath, target);
    }

    public void Upload(string fileName, byte[] content)
    {
        var dir = _options.OutboundDirectory.TrimEnd('/');
        var tmp = $"{dir}/{fileName}.tmp";
        using (var ms = new MemoryStream(content))
        {
            _client.UploadFile(ms, tmp, true);
        }
        _client.RenameFile(tmp, $"{dir}/{fileName}");
    }

    public void Dispose()
    {
        if (_client.IsConnected) _client.Disconnect();
        _client.Dispose();
    }

    private void OnHostKeyReceived(object? sender, HostKeyEventArgs e)
    {
        var expected = _options.HostKeyFingerprint;
        if (string.IsNullOrWhiteSpace(expected))
        {
            e.CanTrust = true;
            return;
        }
        e.CanTrust = MatchesFingerprint(expected, e.HostKey, e.FingerPrint);
        if (!e.CanTrust)
            throw new SshConnectionException($"Host key of {_options.Host} does not match the configured fingerprint.");
    }

    internal static bool MatchesFingerprint(string expected, byte[] hostKey, byte[] md5)
    {
        expected = expected.Trim();
        if (expected.StartsWith("SHA256:", StringComparison.OrdinalIgnoreCase))
        {
            var actual = Convert.ToBase64String(SHA256.HashData(hostKey)).TrimEnd('=');
            return string.Equals(actual, expected[7..].TrimEnd('='), StringComparison.Ordinal);
        }
        var hex = expected.StartsWith("MD5:", StringComparison.OrdinalIgnoreCase) ? expected[4..] : expected;
        var actualMd5 = string.Join(":", md5.Select(b => b.ToString("x2")));
        return string.Equals(actualMd5, hex.Replace("-", ":"), StringComparison.OrdinalIgnoreCase);
    }

    internal static Regex WildcardToRegex(string pattern) =>
        new("^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$", RegexOptions.IgnoreCase);
}
