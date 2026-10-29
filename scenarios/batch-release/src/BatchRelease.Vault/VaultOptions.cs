namespace BatchRelease.Vault;

public sealed class VaultOptions
{
    public const string Section = "Vault";

    /// <summary>Root folder of the record vault on a local NTFS volume.</summary>
    public string Root { get; set; } = @"D:\ReleaseVault";

    /// <summary>Encrypt every stored file with EFS under the service account.</summary>
    public bool RequireEncryption { get; set; } = true;

    /// <summary>Accounts or groups that get read access to vault files besides the service account (e.g. the backup operator group).</summary>
    public List<string> AdditionalReaders { get; set; } = new();

    public string ResourceManagerName { get; set; } = "Proseware Batch Release";

    /// <summary>Security event source registered with <c>install-audit-source</c>.</summary>
    public string AuditSourceName { get; set; } = "Proseware Batch Release";

    public bool SecurityAuditing { get; set; } = true;
}
