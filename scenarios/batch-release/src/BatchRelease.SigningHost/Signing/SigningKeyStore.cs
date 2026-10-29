using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using Microsoft.Extensions.Options;
using Microsoft.Win32.SafeHandles;

namespace BatchRelease.SigningHost.Signing;

public sealed record SignerKeyInfo(string KeyName, string Provider, bool Hardware, RSAParameters PublicKey);

/// <summary>
/// Per-signer RSA keys in a CNG key storage provider. Keys are machine keys that cannot be
/// exported; the key's DACL grants use of the key only to the signer's account, so the
/// signing host can sign only while impersonating the signer.
/// </summary>
public sealed class SigningKeyStore
{
    // NCRYPT_SECURITY_DESCR_PROPERTY / NCRYPT_IMPL_TYPE_PROPERTY
    private const string SecurityDescriptorProperty = "Security Descr";
    private const string ImplementationTypeProperty = "Impl Type";
    private const int DaclSecurityInformation = 0x4;
    private const int ImplementationHardware = 0x1;

    private readonly SigningOptions _options;

    public SigningKeyStore(IOptions<SigningOptions> options) => _options = options.Value;

    public CngProvider Provider => new(_options.KeyStorageProvider);

    public static string KeyNameFor(SecurityIdentifier sid) => $"BatchRelease-QP-{sid.Value}";

    /// <summary>Creates the signer's key. Run elevated (key administrators: SYSTEM and Administrators).</summary>
    public SignerKeyInfo CreateKey(SecurityIdentifier signer, bool replaceExisting)
    {
        var name = KeyNameFor(signer);
        if (CngKey.Exists(name, Provider, CngKeyOpenOptions.MachineKey))
        {
            if (!replaceExisting)
                throw new InvalidOperationException($"Key {name} already exists in {Provider.Provider}.");
            using var old = CngKey.Open(name, Provider, CngKeyOpenOptions.MachineKey);
            old.Delete();
        }

        var dacl = new RawSecurityDescriptor($"O:BAG:SYD:P(A;;GA;;;SY)(A;;GA;;;BA)(A;;GR;;;{signer.Value})");
        var descriptor = new byte[dacl.BinaryLength];
        dacl.GetBinaryForm(descriptor, 0);

        var parameters = new CngKeyCreationParameters
        {
            Provider = Provider,
            KeyCreationOptions = CngKeyCreationOptions.MachineKey,
            ExportPolicy = CngExportPolicies.None,
            KeyUsage = CngKeyUsages.Signing,
        };
        parameters.Parameters.Add(new CngProperty("Length", BitConverter.GetBytes(_options.KeySize), CngPropertyOptions.None));
        parameters.Parameters.Add(new CngProperty(SecurityDescriptorProperty, descriptor, (CngPropertyOptions)DaclSecurityInformation));

        using var key = CngKey.Create(CngAlgorithm.Rsa, name, parameters);
        return Describe(key);
    }

    /// <summary>Reads the public part and provider details of an existing key (elevated).</summary>
    public SignerKeyInfo Inspect(SecurityIdentifier signer)
    {
        using var key = CngKey.Open(KeyNameFor(signer), Provider, CngKeyOpenOptions.MachineKey);
        return Describe(key);
    }

    public void DeleteKey(SecurityIdentifier signer)
    {
        var name = KeyNameFor(signer);
        if (!CngKey.Exists(name, Provider, CngKeyOpenOptions.MachineKey))
            return;
        using var key = CngKey.Open(name, Provider, CngKeyOpenOptions.MachineKey);
        key.Delete();
    }

    /// <summary>
    /// Signs <paramref name="content"/> as CMS SignedData (content attached) with the signer's key.
    /// The key is opened while impersonating the signer's logon token.
    /// </summary>
    public byte[] Sign(SafeAccessTokenHandle signerToken, string keyName, byte[] signerCertificate, byte[] content, DateTime signedUtc)
    {
        return WindowsIdentity.RunImpersonated(signerToken, () =>
        {
            using var key = CngKey.Open(keyName, Provider, CngKeyOpenOptions.MachineKey | CngKeyOpenOptions.Silent);
            using var rsa = new RSACng(key);
            using var publicCert = new X509Certificate2(signerCertificate);
            using var cert = publicCert.CopyWithPrivateKey(rsa);

            var cms = new SignedCms(new ContentInfo(new Oid("1.2.840.113549.1.7.1"), content), detached: false);
            var signer = new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, cert)
            {
                DigestAlgorithm = new Oid("2.16.840.1.101.3.4.2.1"),
                IncludeOption = X509IncludeOption.EndCertOnly,
            };
            signer.SignedAttributes.Add(new Pkcs9SigningTime(signedUtc));
            signer.SignedAttributes.Add(new Pkcs9DocumentDescription("Batch certificate"));
            cms.ComputeSignature(signer, silent: true);
            return cms.Encode();
        });
    }

    public bool IsHardwareKey(CngKey key)
    {
        byte[]? value;
        try
        {
            if (!key.HasProperty(ImplementationTypeProperty, CngPropertyOptions.None))
                return false;
            value = key.GetProperty(ImplementationTypeProperty, CngPropertyOptions.None).GetValue();
        }
        catch (CryptographicException)
        {
            // The software key storage provider does not support the property on keys.
            return false;
        }
        return value is { Length: >= 4 } && (BitConverter.ToInt32(value, 0) & ImplementationHardware) != 0;
    }

    private SignerKeyInfo Describe(CngKey key)
    {
        using var rsa = new RSACng(key);
        return new SignerKeyInfo(key.KeyName!, key.Provider!.Provider, IsHardwareKey(key), rsa.ExportParameters(includePrivateParameters: false));
    }
}
