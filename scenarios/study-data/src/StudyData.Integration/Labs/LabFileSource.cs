using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Renci.SshNet;

namespace StudyData.Integration.Labs;

public record LabFile(string Name, byte[] Content);

public interface ILabFileSource
{
    Task<IReadOnlyList<LabFile>> FetchAsync(CancellationToken ct);

    Task MarkDoneAsync(LabFile file, CancellationToken ct);
}

/// <summary>Central lab drops result files on its SFTP server; processed files are moved to done.</summary>
public sealed class SftpLabFileSource : ILabFileSource
{
    private readonly LabTransferOptions _options;
    private readonly ILogger<SftpLabFileSource> _log;

    public SftpLabFileSource(IOptions<LabTransferOptions> options, ILogger<SftpLabFileSource> log)
    {
        _options = options.Value;
        _log = log;
    }

    private SftpClient Connect()
    {
        var key = new PrivateKeyFile(_options.PrivateKeyFile);
        var client = new SftpClient(new ConnectionInfo(_options.Host, _options.Port, _options.UserName, new PrivateKeyAuthenticationMethod(_options.UserName, key)));
        client.HostKeyReceived += (_, e) =>
        {
            var fingerprint = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(e.HostKey)).TrimEnd('=');
            e.CanTrust = string.IsNullOrEmpty(_options.HostKeySha256) || fingerprint == _options.HostKeySha256.TrimEnd('=');
            if (!e.CanTrust)
            {
                _log.LogError("Central lab host key {Fingerprint} does not match the configured key", fingerprint);
            }
        };
        client.Connect();
        return client;
    }

    public Task<IReadOnlyList<LabFile>> FetchAsync(CancellationToken ct) => Task.Run<IReadOnlyList<LabFile>>(() =>
    {
        using var client = Connect();
        var files = new List<LabFile>();
        foreach (var entry in client.ListDirectory(_options.RemoteFolder).Where(f => f.IsRegularFile && f.Name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)))
        {
            ct.ThrowIfCancellationRequested();
            using var ms = new MemoryStream();
            client.DownloadFile(entry.FullName, ms);
            files.Add(new LabFile(entry.Name, ms.ToArray()));
            File.WriteAllBytes(Path.Combine(_options.LocalFolder, entry.Name), ms.ToArray());
        }

        _log.LogInformation("{Count} central lab file(s) downloaded", files.Count);
        return files;
    }, ct);

    public Task MarkDoneAsync(LabFile file, CancellationToken ct) => Task.Run(() =>
    {
        using var client = Connect();
        client.RenameFile($"{_options.RemoteFolder}/{file.Name}", $"{_options.RemoteDoneFolder}/{file.Name}");
    }, ct);
}

/// <summary>Reads lab files from a folder (development, or when the lab delivers to the share).</summary>
public sealed class FolderLabFileSource : ILabFileSource
{
    private readonly LabTransferOptions _options;

    public FolderLabFileSource(IOptions<LabTransferOptions> options)
    {
        _options = options.Value;
    }

    public async Task<IReadOnlyList<LabFile>> FetchAsync(CancellationToken ct)
    {
        Directory.CreateDirectory(_options.LocalFolder);
        var files = new List<LabFile>();
        foreach (var path in Directory.GetFiles(_options.LocalFolder, "*.csv").OrderBy(p => p, StringComparer.Ordinal))
        {
            files.Add(new LabFile(Path.GetFileName(path), await File.ReadAllBytesAsync(path, ct)));
        }

        return files;
    }

    public Task MarkDoneAsync(LabFile file, CancellationToken ct)
    {
        var done = Path.Combine(_options.LocalFolder, "done");
        Directory.CreateDirectory(done);
        File.Move(Path.Combine(_options.LocalFolder, file.Name), Path.Combine(done, file.Name), overwrite: true);
        return Task.CompletedTask;
    }
}
