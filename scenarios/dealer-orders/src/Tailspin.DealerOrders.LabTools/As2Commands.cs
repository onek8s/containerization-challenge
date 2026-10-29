using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Tailspin.DealerOrders.Edi.As2;

namespace Tailspin.DealerOrders.LabTools
{
    /// <summary>Plays a dealer's AS2 system against the EDI gateway on a test system.</summary>
    internal static class As2Commands
    {
        public static int Certificates(Options options)
        {
            var folder = options.Arg(0) ?? throw new ArgumentException("Give the output folder.");
            var password = options["password", "lab"];
            Directory.CreateDirectory(folder);
            foreach (var name in new[] { "tailspin-as2", "glf-as2" })
            {
                using (var rsa = RSA.Create(2048))
                {
                    var request = new CertificateRequest("CN=" + name + ", O=Lab", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                    request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
                    using (var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(2)))
                    {
                        File.WriteAllBytes(Path.Combine(folder, name + ".pfx"), cert.Export(X509ContentType.Pfx, password));
                        File.WriteAllBytes(Path.Combine(folder, name + ".cer"), cert.Export(X509ContentType.Cert));
                        Console.WriteLine($"{name}: {cert.Thumbprint}");
                    }
                }
            }
            return 0;
        }

        public static int Send(Options options)
        {
            var file = options.Arg(0) ?? throw new ArgumentException("Give the X12 file.");
            var url = options["url", "http://localhost:4080/as2"];
            var from = options["from", "GLFEQUIP"];
            var to = options["to", "TAILSPINMACH"];
            var partner = new X509Certificate2(options["partner-pfx", "certs/glf-as2.pfx"], options["password", "lab"], X509KeyStorageFlags.Exportable);
            var ours = new X509Certificate2(options["our-cer", "certs/tailspin-as2.cer"]);
            var encrypt = options["encrypt", "true"] == "true";

            var codec = new As2Codec(partner) { ProductName = "Lab dealer AS2" };
            var message = codec.Pack(File.ReadAllBytes(file), "application/EDI-X12", Path.GetFileName(file), from, to, ours,
                sign: true, encrypt: encrypt, mdnTo: "edi@" + from.ToLowerInvariant() + ".example", signedMdn: true);

            using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) })
            {
                var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new ByteArrayContent(message.Body) };
                request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(message.ContentType);
                foreach (var h in message.Headers.Where(h => !h.Key.Equals("Content-Transfer-Encoding", StringComparison.OrdinalIgnoreCase)))
                    request.Headers.TryAddWithoutValidation(h.Key, h.Value);
                using (var response = http.SendAsync(request).GetAwaiter().GetResult())
                {
                    var body = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                    Console.WriteLine($"HTTP {(int)response.StatusCode}, {body.Length} bytes, {response.Content.Headers.ContentType}");
                    if (!response.IsSuccessStatusCode || body.Length == 0) return 4;
                    var mdn = codec.ReadMdn(response.Content.Headers.ContentType.ToString(), body, ours);
                    Console.WriteLine($"MDN for {mdn.OriginalMessageId}: {mdn.Disposition}");
                    Console.WriteLine($"MIC {(mdn.ReceivedMic == message.Mic ? "matches" : "DOES NOT MATCH")}, signature {(mdn.SignatureVerified ? "verified" : "not signed")}");
                    return mdn.Processed && mdn.ReceivedMic == message.Mic ? 0 : 5;
                }
            }
        }
    }
}
