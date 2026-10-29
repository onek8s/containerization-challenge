namespace Litware.DocControl.Data.Storage;

public sealed class StorageOptions
{
    public const string SectionName = "DocControl:Storage";

    /// <summary>Root of the document vault, for example <c>\\files01.corp.local\DocVault$</c>.</summary>
    public string VaultRoot { get; set; } = "";

    /// <summary>Where issued transmittal packages are published, for example <c>\\files01.corp.local\Transmittals$</c>.</summary>
    public string TransmittalRoot { get; set; } = "";

    /// <summary>Drop share scanned by the indexer, one folder per project.</summary>
    public string DropRoot { get; set; } = "";
}
