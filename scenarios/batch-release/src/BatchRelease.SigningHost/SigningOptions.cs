namespace BatchRelease.SigningHost;

public sealed class SigningOptions
{
    public const string Section = "Signing";

    /// <summary>CNG key storage provider that holds the Qualified Persons' signing keys.</summary>
    public string KeyStorageProvider { get; set; } = "Microsoft Platform Crypto Provider";

    /// <summary>Refuse to enrol or sign with keys that the provider does not report as hardware keys.</summary>
    public bool RequireHardwareKeys { get; set; } = true;

    /// <summary>Thumbprint of the signing CA certificate in LocalMachine\My that issues signer certificates.</summary>
    public string IssuerThumbprint { get; set; } = "";

    public int CertificateValidityMonths { get; set; } = 36;

    public int KeySize { get; set; } = 2048;

    public string Manufacturer { get; set; } = "Proseware Pharmaceuticals Oy";

    /// <summary>Windows accounts (application pool identities) allowed to call the signing host.</summary>
    public List<string> TrustedCallers { get; set; } = new();

    /// <summary>Windows domain or computer name used when an account name is given without one.</summary>
    public string DefaultDomain { get; set; } = "";
}
