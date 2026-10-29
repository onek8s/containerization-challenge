using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Options;

namespace BatchRelease.SigningHost.Signing;

/// <summary>Issues signer certificates from the signing CA held on this host.</summary>
public sealed class SignerCertificates
{
    private const string DocumentSigningEku = "1.3.6.1.4.1.311.10.3.12";

    private readonly SigningOptions _options;

    public SignerCertificates(IOptions<SigningOptions> options) => _options = options.Value;

    public X509Certificate2 Issue(string displayName, string accountName, RSAParameters publicKey)
    {
        using var issuer = FindIssuer();
        using var rsa = RSA.Create(publicKey);

        var subject = new X500DistinguishedNameBuilder();
        subject.AddCommonName(displayName);
        subject.AddOrganizationalUnitName("Qualified Person");
        subject.AddOrganizationName(_options.Manufacturer);
        subject.AddCountryOrRegion("FI");

        var request = new CertificateRequest(subject.Build(), rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid(DocumentSigningEku) }, false));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        request.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromCertificate(issuer, includeKeyIdentifier: true, includeIssuerAndSerial: false));

        var san = new SubjectAlternativeNameBuilder();
        san.AddUserPrincipalName(accountName);
        request.CertificateExtensions.Add(san.Build());

        var notBefore = DateTimeOffset.UtcNow.AddMinutes(-5);
        var notAfter = notBefore.AddMonths(_options.CertificateValidityMonths);
        if (notAfter > issuer.NotAfter)
            notAfter = issuer.NotAfter.ToUniversalTime();

        var serial = RandomNumberGenerator.GetBytes(16);
        serial[0] &= 0x7F;
        return request.Create(issuer, notBefore, notAfter, serial);
    }

    public X509Certificate2 FindIssuer()
    {
        if (string.IsNullOrWhiteSpace(_options.IssuerThumbprint))
            throw new InvalidOperationException("Signing:IssuerThumbprint is not configured.");

        using var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
        store.Open(OpenFlags.ReadOnly);
        var found = store.Certificates.Find(X509FindType.FindByThumbprint, _options.IssuerThumbprint.Replace(" ", ""), validOnly: false);
        if (found.Count == 0)
            throw new InvalidOperationException($"Signing CA certificate {_options.IssuerThumbprint} is not in LocalMachine\\My.");
        var issuer = found[0];
        if (!issuer.HasPrivateKey)
            throw new InvalidOperationException($"Signing CA certificate {issuer.Subject} has no private key on this machine.");
        return issuer;
    }
}
