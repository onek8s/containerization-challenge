using VanArsdel.Integration.Domain.Partners;
using VanArsdel.Integration.LabStubs.Simulators;

namespace VanArsdel.Integration.LabStubs.Lab
{
    /// <summary>
    /// The partner send ports, addressed by the routing slip's endpoint name (AS2.C1001, SFTP.C1002, FTP.C2002):
    /// AS2 posts to the partner's station and checks the MDN, SFTP and FTP write to the partner's folder through a
    /// temporary name like the adapters' temporary-folder option.
    /// </summary>
    public class LabTransport
    {
        private readonly LabContext _lab;
        private readonly HttpClient _http;
        private readonly Func<string> _baseUrl;

        public LabTransport(LabContext lab, HttpClient http, Func<string> baseUrl)
        {
            _lab = lab;
            _http = http;
            _baseUrl = baseUrl;
        }

        public string Deliver(string endpoint, TradingPartner partner, byte[] body, string fileName)
        {
            var dot = endpoint.IndexOf('.');
            var kind = dot < 0 ? endpoint : endpoint.Substring(0, dot);
            var target = dot < 0 ? partner.PartnerId : endpoint.Substring(dot + 1);
            if (!string.Equals(target, partner.PartnerId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Endpoint " + endpoint + " does not belong to " + partner.PartnerId);
            }

            switch (kind.ToUpperInvariant())
            {
                case "AS2":
                    var receipt = As2.Post(_http, _baseUrl() + "/partners/" + Uri.EscapeDataString(partner.As2Identifier) + "/as2", As2.OurAs2Id, partner.As2Identifier, body, fileName);
                    _lab.Trace("send port {0}: {1} delivered, {2}", endpoint, fileName, As2.Describe(receipt));
                    return receipt.MessageId;
                case "SFTP":
                case "FTP":
                    var folder = Directory.CreateDirectory(_lab.WorkPath(kind.ToLowerInvariant(), partner.PartnerId, "outbound")).FullName;
                    var temp = Path.Combine(folder, fileName + ".tmp");
                    File.WriteAllBytes(temp, body);
                    File.Move(temp, Path.Combine(folder, fileName), true);
                    _lab.Trace("send port {0}: {1} uploaded", endpoint, fileName);
                    return Path.Combine(folder, fileName);
                case "FILE":
                    var outFolder = Directory.CreateDirectory(_lab.WorkPath("out", partner.PartnerId)).FullName;
                    File.WriteAllBytes(Path.Combine(outFolder, fileName), body);
                    return Path.Combine(outFolder, fileName);
                default:
                    throw new NotSupportedException("No send port for endpoint " + endpoint);
            }
        }
    }
}
