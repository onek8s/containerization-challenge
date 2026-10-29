using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using BatchRelease.Core.Certificates;

namespace BatchRelease.Tests;

/// <summary>Checks how a receiver (ERP) verifies a batch certificate and reads its content.</summary>
public class CmsTests
{
    [Fact]
    public void Receiver_verifies_signature_and_reads_content()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=Anna Virtanen, OU=Qualified Person, O=Proseware Pharmaceuticals Oy", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));

        var payload = CertificateSerializer.Serialize(new ReleaseCertificateContent
        {
            CertificateNumber = "RC-TKU-2026-00042",
            Decision = "RELEASED",
            BatchNumber = "T2600418",
            ProductCode = "PRX-0450",
            ExpiresOn = new DateOnly(2029, 8, 31),
            QualifiedPerson = "Anna Virtanen",
            SignedUtc = new DateTime(2026, 10, 29, 9, 15, 30, DateTimeKind.Utc),
        });

        var cms = new SignedCms(new ContentInfo(payload), detached: false);
        cms.ComputeSignature(new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, cert) { IncludeOption = X509IncludeOption.EndCertOnly });
        var encoded = cms.Encode();

        var received = new SignedCms();
        received.Decode(encoded);
        received.CheckSignature(verifySignatureOnly: true);
        Assert.Equal(cert.Thumbprint, received.SignerInfos[0].Certificate!.Thumbprint);

        var summary = CertificateSerializer.ReadSummary(received.ContentInfo.Content);
        Assert.Equal("T2600418", summary.BatchNumber);

        encoded[^10] ^= 0xFF;
        var tampered = new SignedCms();
        Assert.ThrowsAny<CryptographicException>(() =>
        {
            tampered.Decode(encoded);
            tampered.CheckSignature(verifySignatureOnly: true);
        });
    }
}
