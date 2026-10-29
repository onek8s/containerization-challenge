using Microsoft.Extensions.Options;
using Renci.SshNet;

namespace Humongous.PolicyService.Bordereau;

public sealed class BordereauOptions
{
    public const string SectionName = "Bordereau";

    /// <summary>Unique market reference of the binding authority.</summary>
    public string Umr { get; set; } = "B1234HUM2024BC";

    /// <summary><c>Sftp</c> or <c>Folder</c>.</summary>
    public string Delivery { get; set; } = "Sftp";

    public string FolderPath { get; set; } = "";

    public SftpOptions Sftp { get; set; } = new();
}

public sealed class SftpOptions
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 22;
    public string Username { get; set; } = "";
    public string PrivateKeyPath { get; set; } = "";
    public string? PrivateKeyPassphrase { get; set; }

    /// <summary>Expected host key fingerprint, SHA256 base64 as printed by ssh-keygen -l.</summary>
    public string HostKeyFingerprint { get; set; } = "";

    public string RemoteFolder { get; set; } = "/inbound";
}

public interface IBordereauDelivery
{
    Task DeliverAsync(string fileName, byte[] content, CancellationToken ct);
}

public sealed class FolderDelivery(IOptions<BordereauOptions> options, ILogger<FolderDelivery> logger) : IBordereauDelivery
{
    public async Task DeliverAsync(string fileName, byte[] content, CancellationToken ct)
    {
        var folder = options.Value.FolderPath;
        Directory.CreateDirectory(folder);
        var target = Path.Combine(folder, fileName);
        await File.WriteAllBytesAsync(target + ".tmp", content, ct);
        File.Move(target + ".tmp", target, overwrite: true);
        logger.LogInformation("Bordereau written to {Path}", target);
    }
}

/// <summary>Uploads the bordereau to the capacity provider's SFTP server with key authentication.</summary>
public sealed class SftpDelivery(IOptions<BordereauOptions> options, ILogger<SftpDelivery> logger) : IBordereauDelivery
{
    public Task DeliverAsync(string fileName, byte[] content, CancellationToken ct)
    {
        var sftp = options.Value.Sftp;
        using var key = string.IsNullOrEmpty(sftp.PrivateKeyPassphrase)
            ? new PrivateKeyFile(sftp.PrivateKeyPath)
            : new PrivateKeyFile(sftp.PrivateKeyPath, sftp.PrivateKeyPassphrase);
        var connection = new ConnectionInfo(sftp.Host, sftp.Port, sftp.Username, new PrivateKeyAuthenticationMethod(sftp.Username, key))
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        using var client = new SftpClient(connection);
        client.HostKeyReceived += (_, e) =>
        {
            e.CanTrust = string.Equals(e.FingerPrintSHA256, sftp.HostKeyFingerprint.Replace("SHA256:", ""), StringComparison.Ordinal);
            if (!e.CanTrust)
                logger.LogError("SFTP host key {Fingerprint} for {Host} does not match the configured fingerprint", e.FingerPrintSHA256, sftp.Host);
        };
        client.Connect();
        try
        {
            var remote = $"{sftp.RemoteFolder.TrimEnd('/')}/{fileName}";
            // Upload under a temporary name and rename, so the provider's import never sees half a file.
            using (var stream = new MemoryStream(content))
                client.UploadFile(stream, remote + ".part", canOverride: true);
            if (client.Exists(remote)) client.DeleteFile(remote);
            client.RenameFile(remote + ".part", remote);
            logger.LogInformation("Bordereau uploaded to sftp://{Host}{Remote}", sftp.Host, remote);
        }
        finally
        {
            client.Disconnect();
        }
        return Task.CompletedTask;
    }
}
