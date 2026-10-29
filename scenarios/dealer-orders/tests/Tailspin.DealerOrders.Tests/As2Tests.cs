using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Tailspin.DealerOrders.Edi.As2;
using Xunit;

namespace Tailspin.DealerOrders.Tests
{
    public class As2Tests
    {
        private static X509Certificate2 Certificate(string name)
        {
            using (var rsa = RSA.Create(2048))
            {
                var request = new CertificateRequest("CN=" + name, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
                var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
                return new X509Certificate2(cert.Export(X509ContentType.Pfx, "x"), "x", X509KeyStorageFlags.Exportable);
            }
        }

        [Fact]
        public void Signed_and_encrypted_message_round_trips_and_mdn_mic_matches()
        {
            var ours = Certificate("tailspin-as2");
            var theirs = Certificate("glf-as2");
            var theirsPublic = new X509Certificate2(theirs.Export(X509ContentType.Cert));
            var oursPublic = new X509Certificate2(ours.Export(X509ContentType.Cert));
            var payload = Encoding.ASCII.GetBytes(Lab.Sample("edi/850-glf-machine.x12"));

            // Dealer sends to us.
            var sender = new As2Codec(theirs);
            var message = sender.Pack(payload, "application/edi-x12", "po.x12", "GLFEQUIP", "TAILSPIN", oursPublic, sign: true, encrypt: true, mdnTo: "edi@glf.example", signedMdn: true);
            Assert.StartsWith("application/pkcs7-mime", message.ContentType);

            var receiver = new As2Codec(ours);
            var headers = new Dictionary<string, string>(message.Headers, StringComparer.OrdinalIgnoreCase) { ["Content-Type"] = message.ContentType };
            var inbound = receiver.Unpack(headers, message.Body, id => id == "GLFEQUIP" ? theirsPublic : null);
            Assert.True(inbound.WasEncrypted);
            Assert.True(inbound.WasSigned);
            Assert.Equal(payload, inbound.Payload);
            Assert.Equal(message.Mic, inbound.Mic);
            Assert.True(inbound.SignedMdnRequested);

            var mdn = receiver.CreateMdn(inbound, "TAILSPIN", processed: true, errorText: null);
            var result = sender.ReadMdn(mdn.ContentType, mdn.Body, oursPublic);
            Assert.True(result.Processed);
            Assert.True(result.SignatureVerified);
            Assert.Equal(message.Mic, result.ReceivedMic);
            Assert.Equal(message.MessageId, result.OriginalMessageId);
        }

        [Fact]
        public void Tampered_signed_message_is_rejected()
        {
            var ours = Certificate("tailspin-as2");
            var theirs = Certificate("ntm-as2");
            var theirsPublic = new X509Certificate2(theirs.Export(X509ContentType.Cert));
            var message = new As2Codec(theirs).Pack(Encoding.ASCII.GetBytes("ISA*..."), "application/edi-x12", "x.edi", "NTM", "TAILSPIN", null, sign: true, encrypt: false, mdnTo: null, signedMdn: false);
            var body = Encoding.GetEncoding("ISO-8859-1").GetString(message.Body).Replace("ISA*...", "ISA*!!!");
            var headers = new Dictionary<string, string>(message.Headers, StringComparer.OrdinalIgnoreCase) { ["Content-Type"] = message.ContentType };
            Assert.ThrowsAny<CryptographicException>(() => new As2Codec(ours).Unpack(headers, Encoding.GetEncoding("ISO-8859-1").GetBytes(body), _ => theirsPublic));
        }

        [Fact]
        public void Error_mdn_carries_the_error_disposition()
        {
            var inbound = new As2Inbound { MessageId = "<1@x>", From = "GLF", MicAlgorithm = "sha-256", Mic = "abc=" };
            var codec = new As2Codec(null);
            var mdn = codec.CreateMdn(inbound, "TAILSPIN", processed: false, errorText: "decryption-failed");
            var result = codec.ReadMdn(mdn.ContentType, mdn.Body, null);
            Assert.False(result.Processed);
            Assert.Contains("decryption-failed", result.Disposition);
        }
    }
}
