using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Tailspin.DealerOrders.Edi.As2
{
    public class As2Outbound
    {
        public Dictionary<string, string> Headers { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public byte[] Body { get; set; }
        public string ContentType { get; set; }
        public string MessageId { get; set; }

        /// <summary>MIC of the signed content, compared with the Received-Content-MIC of the returned MDN.</summary>
        public string Mic { get; set; }
    }

    public class As2Inbound
    {
        public string MessageId { get; set; }
        public string From { get; set; }
        public string To { get; set; }
        public byte[] Payload { get; set; }
        public string PayloadContentType { get; set; }
        public bool WasEncrypted { get; set; }
        public bool WasSigned { get; set; }
        public string Mic { get; set; }
        public string MicAlgorithm { get; set; }
        public bool SignedMdnRequested { get; set; }
        public string ReceiptDeliveryOption { get; set; }
    }

    /// <summary>
    /// AS2 (RFC 4130) packaging: S/MIME signing (detached PKCS#7, SHA-256), encryption (3DES or AES-256 enveloped data),
    /// MIC calculation and MDN creation and verification.
    /// </summary>
    public class As2Codec
    {
        private readonly X509Certificate2 _ownCertificate;

        public As2Codec(X509Certificate2 ownCertificate)
        {
            _ownCertificate = ownCertificate;
        }

        public string ProductName { get; set; } = "Tailspin Dealer Orders EDI Gateway";

        public As2Outbound Pack(byte[] payload, string contentType, string fileName, string from, string to, X509Certificate2 partnerCertificate,
            bool sign, bool encrypt, string mdnTo, bool signedMdn, string asyncMdnUrl = null)
        {
            var content = new MimeEntity { ContentType = contentType, Body = payload };
            content.SetHeader("Content-Transfer-Encoding", "binary");
            content.SetHeader("Content-Disposition", $"attachment; filename=\"{fileName}\"");
            var contentBytes = content.ToBytes();
            var mic = ComputeMic(contentBytes, "sha-256");

            MimeEntity outer = content;
            if (sign)
            {
                var signature = SignDetached(contentBytes);
                var sigPart = new MimeEntity { ContentType = "application/pkcs7-signature; name=smime.p7s", Body = Encoding.ASCII.GetBytes(Convert.ToBase64String(signature, Base64FormattingOptions.InsertLineBreaks) + "\r\n") };
                sigPart.SetHeader("Content-Transfer-Encoding", "base64");
                sigPart.SetHeader("Content-Disposition", "attachment; filename=smime.p7s");
                outer = MimeEntity.Multipart("signed", MimeEntity.NewBoundary(), "protocol=\"application/pkcs7-signature\"; micalg=sha-256", contentBytes, sigPart.ToBytes());
            }

            if (encrypt)
            {
                if (partnerCertificate == null) throw new InvalidOperationException("Encryption needs the partner certificate.");
                var toEncrypt = sign ? WithoutMimeVersion(outer).ToBytes() : contentBytes;
                var envelope = new EnvelopedCms(new ContentInfo(toEncrypt), new AlgorithmIdentifier(new Oid("2.16.840.1.101.3.4.1.42")));
                envelope.Encrypt(new CmsRecipient(SubjectIdentifierType.IssuerAndSerialNumber, partnerCertificate));
                outer = new MimeEntity { ContentType = "application/pkcs7-mime; smime-type=enveloped-data; name=smime.p7m", Body = envelope.Encode() };
                outer.SetHeader("Content-Transfer-Encoding", "binary");
            }

            var message = new As2Outbound
            {
                MessageId = "<" + Guid.NewGuid().ToString("N") + "@" + from + ">",
                Mic = mic
            };
            message.Headers["AS2-Version"] = "1.2";
            message.Headers["AS2-From"] = Quote(from);
            message.Headers["AS2-To"] = Quote(to);
            message.Headers["Message-ID"] = message.MessageId;
            message.Headers["Subject"] = "EDI " + fileName;
            message.Headers["Date"] = DateTime.UtcNow.ToString("r");
            message.Headers["MIME-Version"] = "1.0";
            if (!string.IsNullOrEmpty(mdnTo))
            {
                message.Headers["Disposition-Notification-To"] = mdnTo;
                if (signedMdn)
                    message.Headers["Disposition-Notification-Options"] = "signed-receipt-protocol=optional, pkcs7-signature; signed-receipt-micalg=optional, sha-256";
                if (!string.IsNullOrEmpty(asyncMdnUrl)) message.Headers["Receipt-Delivery-Option"] = asyncMdnUrl;
            }

            message.ContentType = outer.ContentType;
            if (outer.Header("Content-Transfer-Encoding") != null) message.Headers["Content-Transfer-Encoding"] = outer.Header("Content-Transfer-Encoding");
            message.Body = outer.Body;
            return message;
        }

        public As2Inbound Unpack(IDictionary<string, string> headers, byte[] body, Func<string, X509Certificate2> partnerCertificateByAs2Id)
        {
            var result = new As2Inbound
            {
                MessageId = Get(headers, "Message-ID"),
                From = Unquote(Get(headers, "AS2-From")),
                To = Unquote(Get(headers, "AS2-To")),
                ReceiptDeliveryOption = Get(headers, "Receipt-Delivery-Option")
            };
            var options = Get(headers, "Disposition-Notification-Options") ?? "";
            result.SignedMdnRequested = options.IndexOf("pkcs7-signature", StringComparison.OrdinalIgnoreCase) >= 0;
            result.MicAlgorithm = options.IndexOf("sha-256", StringComparison.OrdinalIgnoreCase) >= 0 || options.Length == 0 ? "sha-256" : "sha1";

            var entity = new MimeEntity { ContentType = Get(headers, "Content-Type"), Body = body };
            if (entity.MediaType == "application/pkcs7-mime" || entity.MediaType == "application/x-pkcs7-mime")
            {
                var envelope = new EnvelopedCms();
                envelope.Decode(body);
                envelope.Decrypt(new X509Certificate2Collection(_ownCertificate));
                entity = MimeEntity.Parse(envelope.ContentInfo.Content);
                result.WasEncrypted = true;
            }

            if (entity.MediaType == "multipart/signed")
            {
                var parts = entity.RawParts();
                if (parts.Count != 2) throw new CryptographicException("Signed message must have two parts.");
                var signaturePart = MimeEntity.Parse(parts[1]);
                var signature = Convert.FromBase64String(Encoding.ASCII.GetString(signaturePart.Body));
                var cms = new SignedCms(new ContentInfo(parts[0]), true);
                cms.Decode(signature);
                var partnerCertificate = partnerCertificateByAs2Id(result.From)
                    ?? throw new CryptographicException($"No certificate configured for AS2 partner {result.From}.");
                cms.CheckSignature(new X509Certificate2Collection(partnerCertificate), true);
                result.WasSigned = true;
                result.Mic = ComputeMic(parts[0], result.MicAlgorithm);
                entity = MimeEntity.Parse(parts[0]);
            }
            else
            {
                result.Mic = ComputeMic(entity.ToBytes(), result.MicAlgorithm);
            }

            result.Payload = entity.Body;
            result.PayloadContentType = entity.ContentType;
            return result;
        }

        /// <summary>Builds the MDN returned for a received message (synchronously in the HTTP response).</summary>
        public As2Outbound CreateMdn(As2Inbound received, string ourAs2Id, bool processed, string errorText)
        {
            var boundary = MimeEntity.NewBoundary();
            var text = new MimeEntity { ContentType = "text/plain; charset=us-ascii", Body = Encoding.ASCII.GetBytes(processed
                ? $"The message {received.MessageId} was received and processed.\r\n"
                : $"The message {received.MessageId} could not be processed: {errorText}\r\n") };
            var disposition = new StringBuilder()
                .Append("Reporting-UA: ").Append(ProductName).Append("\r\n")
                .Append("Original-Recipient: rfc822; ").Append(ourAs2Id).Append("\r\n")
                .Append("Final-Recipient: rfc822; ").Append(ourAs2Id).Append("\r\n")
                .Append("Original-Message-ID: ").Append(received.MessageId).Append("\r\n")
                .Append("Disposition: automatic-action/MDN-sent-automatically; ")
                .Append(processed ? "processed" : "processed/error: " + (errorText ?? "unexpected-processing-error")).Append("\r\n");
            if (received.Mic != null) disposition.Append("Received-Content-MIC: ").Append(received.Mic).Append(", ").Append(received.MicAlgorithm).Append("\r\n");
            var report = new MimeEntity { ContentType = "message/disposition-notification", Body = Encoding.ASCII.GetBytes(disposition.ToString()) };
            report.SetHeader("Content-Transfer-Encoding", "7bit");

            var mdn = MimeEntity.Multipart("report", boundary, "report-type=disposition-notification", text.ToBytes(), report.ToBytes());
            MimeEntity outer = mdn;
            if (received.SignedMdnRequested && _ownCertificate != null)
            {
                var mdnBytes = WithoutMimeVersion(mdn).ToBytes();
                var signature = SignDetached(mdnBytes);
                var sigPart = new MimeEntity { ContentType = "application/pkcs7-signature; name=smime.p7s", Body = Encoding.ASCII.GetBytes(Convert.ToBase64String(signature, Base64FormattingOptions.InsertLineBreaks) + "\r\n") };
                sigPart.SetHeader("Content-Transfer-Encoding", "base64");
                outer = MimeEntity.Multipart("signed", MimeEntity.NewBoundary(), "protocol=\"application/pkcs7-signature\"; micalg=sha-256", mdnBytes, sigPart.ToBytes());
            }

            var result = new As2Outbound { ContentType = outer.ContentType, Body = outer.Body, MessageId = "<" + Guid.NewGuid().ToString("N") + "@" + ourAs2Id + ">" };
            result.Headers["AS2-Version"] = "1.2";
            result.Headers["AS2-From"] = Quote(ourAs2Id);
            result.Headers["AS2-To"] = Quote(received.From);
            result.Headers["Message-ID"] = result.MessageId;
            result.Headers["MIME-Version"] = "1.0";
            return result;
        }

        /// <summary>Reads an MDN received for one of our messages. Returns the disposition and the MIC it reports.</summary>
        public MdnResult ReadMdn(string contentType, byte[] body, X509Certificate2 partnerCertificate)
        {
            var entity = new MimeEntity { ContentType = contentType, Body = body };
            var result = new MdnResult();
            if (entity.MediaType == "multipart/signed")
            {
                var parts = entity.RawParts();
                var signaturePart = MimeEntity.Parse(parts[1]);
                var cms = new SignedCms(new ContentInfo(parts[0]), true);
                cms.Decode(Convert.FromBase64String(Encoding.ASCII.GetString(signaturePart.Body)));
                if (partnerCertificate != null)
                {
                    cms.CheckSignature(new X509Certificate2Collection(partnerCertificate), true);
                    result.SignatureVerified = true;
                }
                entity = MimeEntity.Parse(parts[0]);
            }

            if (entity.MediaType != "multipart/report") throw new FormatException("MDN is not a multipart/report.");
            foreach (var raw in entity.RawParts())
            {
                var part = MimeEntity.Parse(raw);
                if (part.MediaType != "message/disposition-notification") continue;
                foreach (var line in Encoding.ASCII.GetString(part.Body).Replace("\r\n", "\n").Split('\n'))
                {
                    var idx = line.IndexOf(':');
                    if (idx < 0) continue;
                    var name = line.Substring(0, idx).Trim();
                    var value = line.Substring(idx + 1).Trim();
                    if (name.Equals("Disposition", StringComparison.OrdinalIgnoreCase)) result.Disposition = value;
                    else if (name.Equals("Original-Message-ID", StringComparison.OrdinalIgnoreCase)) result.OriginalMessageId = value;
                    else if (name.Equals("Received-Content-MIC", StringComparison.OrdinalIgnoreCase)) result.ReceivedMic = value.Split(',')[0].Trim();
                }
            }
            return result;
        }

        public static string ComputeMic(byte[] content, string algorithm)
        {
            using (HashAlgorithm hash = string.Equals(algorithm, "sha1", StringComparison.OrdinalIgnoreCase) ? (HashAlgorithm)SHA1.Create() : SHA256.Create())
                return Convert.ToBase64String(hash.ComputeHash(content));
        }

        private byte[] SignDetached(byte[] content)
        {
            if (_ownCertificate == null || !_ownCertificate.HasPrivateKey)
                throw new InvalidOperationException("Signing needs our certificate with its private key.");
            var cms = new SignedCms(new ContentInfo(content), true);
            var signer = new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, _ownCertificate) { DigestAlgorithm = new Oid("2.16.840.1.101.3.4.2.1") };
            signer.SignedAttributes.Add(new Pkcs9SigningTime(DateTime.UtcNow));
            cms.ComputeSignature(signer);
            return cms.Encode();
        }

        private static MimeEntity WithoutMimeVersion(MimeEntity entity)
        {
            var copy = new MimeEntity { Body = entity.Body };
            foreach (var h in entity.Headers)
                if (!string.Equals(h.Key, "MIME-Version", StringComparison.OrdinalIgnoreCase)) copy.Headers.Add(h);
            return copy;
        }

        private static string Get(IDictionary<string, string> headers, string name) =>
            headers.TryGetValue(name, out var value) ? value : null;

        private static string Quote(string id) => id.IndexOf(' ') >= 0 ? "\"" + id + "\"" : id;

        private static string Unquote(string id) => id?.Trim().Trim('"');
    }

    public class MdnResult
    {
        public string OriginalMessageId { get; set; }
        public string Disposition { get; set; }
        public string ReceivedMic { get; set; }
        public bool SignatureVerified { get; set; }
        public bool Processed => Disposition != null && Disposition.EndsWith("processed", StringComparison.OrdinalIgnoreCase);
    }
}
