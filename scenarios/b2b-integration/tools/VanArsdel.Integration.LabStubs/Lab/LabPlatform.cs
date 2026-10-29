using System.Xml;
using VanArsdel.Integration.Contracts;
using VanArsdel.Integration.Domain.Partners;
using VanArsdel.Integration.LabStubs.Pipelines;
using VanArsdel.Integration.LabStubs.Simulators;
using VanArsdel.Integration.Messaging;

namespace VanArsdel.Integration.LabStubs.Lab
{
    /// <summary>A receive location of the bindings: transport, pipeline and the receive port it belongs to.</summary>
    public class LabReceiveLocation
    {
        public LabReceiveLocation(string name, string port, string pipeline, string transport)
        {
            Name = name;
            Port = port;
            Pipeline = pipeline;
            Transport = transport;
        }

        public string Name { get; }
        public string Port { get; }
        public string Pipeline { get; }
        public string Transport { get; }
    }

    /// <summary>Inbound map of a receive port and the MessageKind the port's map leaves on the message.</summary>
    public class LabPortMap
    {
        public string SourceRoot { get; set; }
        public string Map { get; set; }
        public Func<TradingPartner, XmlDocument, string> Kind { get; set; }
    }

    /// <summary>
    /// Plays the BizTalk group: receive locations run their pipeline, the receive port applies its inbound map,
    /// and the message box hands each message to the orchestration whose activating receive subscribes to it.
    /// Failures end as suspended messages (work/suspended), the way they would in the group hub.
    /// </summary>
    public class LabPlatform
    {
        public static readonly IReadOnlyList<LabReceiveLocation> Locations = new[]
        {
            new LabReceiveLocation("rl_Customer_EDI_AS2", "rp_Customer_EDI_AS2", "As2EdiReceive", "AS2"),
            new LabReceiveLocation("rl_Customer_EDI_SFTP", "rp_Customer_EDI_FILE", "FileEdiReceive", "SFTP"),
            new LabReceiveLocation("rl_Customer_EDI_FTP", "rp_Customer_EDI_FILE", "FileEdiReceive", "FTP"),
            new LabReceiveLocation("rl_Supplier_EDI_SFTP", "rp_Supplier_EDI", "FileEdiReceive", "SFTP"),
            new LabReceiveLocation("rl_Gateway_Orders_WCF", "rp_Gateway_Orders", "PartnerXmlReceive", "WCF-BasicHttp"),
            new LabReceiveLocation("rl_Erp_IDoc_FILE", "rp_Erp_IDoc", "IDocReceive", "FILE")
        };

        private static readonly LabPortMap[] PortMaps =
        {
            new LabPortMap { SourceRoot = "X12_00401_850", Map = "X12_00401_850_to_Order", Kind = (p, d) => "CustomerOrder" },
            new LabPortMap { SourceRoot = "EFACT_D96A_ORDERS", Map = "EFACT_D96A_ORDERS_to_Order", Kind = (p, d) => "CustomerOrder" },
            new LabPortMap { SourceRoot = "X12_00401_810", Map = "X12_00401_810_to_Invoice", Kind = (p, d) => Canonical(d, "Kind") },
            new LabPortMap { SourceRoot = "EFACT_D96A_INVOIC", Map = "EFACT_D96A_INVOIC_to_Invoice", Kind = (p, d) => Canonical(d, "Kind") },
            new LabPortMap { SourceRoot = "X12_00401_997", Map = "X12_00401_997_to_FunctionalAck", Kind = AckKind },
            new LabPortMap { SourceRoot = "EFACT_D3_CONTRL", Map = "EFACT_D3_CONTRL_to_FunctionalAck", Kind = AckKind },
            new LabPortMap { SourceRoot = "Order", Map = "PartnerOrder_to_Order", Kind = (p, d) => "CustomerOrder" },
            new LabPortMap { SourceRoot = "DESADV01", Map = "IDoc_DESADV01_to_Despatch", Kind = (p, d) => "Despatch" },
            new LabPortMap { SourceRoot = "INVOIC02", Map = "IDoc_INVOIC02_to_Invoice", Kind = (p, d) => "CustomerInvoice" },
            new LabPortMap { SourceRoot = "ORDERS05", Map = null, Kind = (p, d) => "ORDERS" }
        };

        private readonly LabContext _lab;

        public LabPlatform(LabContext lab, PipelineRunner pipelines, LabTransport transport)
        {
            _lab = lab;
            Pipelines = pipelines;
            Transport = transport;
        }

        public PipelineRunner Pipelines { get; }
        public LabTransport Transport { get; }
        public LabOrchestrations Orchestrations { get; set; }
        public int Published { get; private set; }

        public static LabReceiveLocation Location(string name)
        {
            return Locations.First(l => l.Name == name);
        }

        /// <summary>Runs a receive location on one inbound file or request body.</summary>
        public ReceiveResult Receive(string locationName, byte[] body, MessageContext context)
        {
            var location = Location(locationName);
            context.Promote(ContextProperties.ReceivePortName, ContextProperties.SystemNamespace, location.Port);
            context.Promote("ReceiveLocationName", ContextProperties.SystemNamespace, location.Name);
            context.Promote("InboundTransportType", ContextProperties.SystemNamespace, location.Transport);
            context.Write(ContextProperties.InterchangeID, ContextProperties.SystemNamespace, "{" + Guid.NewGuid().ToString().ToUpperInvariant() + "}");

            ReceiveResult result;
            try
            {
                result = Pipelines.Receive(location.Pipeline, body, context);
            }
            catch (PipelineException ex)
            {
                _lab.Suspend(location.Name + " (" + location.Pipeline + ")", ex, body);
                return null;
            }

            _lab.Trace("{0} [{1}]: {2}", location.Name, location.Pipeline, string.Join("; ", result.Steps));
            foreach (var ack in result.Acks)
            {
                Orchestrations.SendGeneratedAck(result.Partner, ack);
            }

            foreach (var message in result.Messages)
            {
                Publish(message, result.Partner);
            }

            return result;
        }

        /// <summary>Receive port map, then the message box subscription.</summary>
        public void Publish(PipelineMessage message, TradingPartner partner)
        {
            var doc = new XmlDocument { PreserveWhitespace = false };
            try
            {
                doc.Load(message.OpenBody());
                var root = doc.DocumentElement.LocalName;
                var portMap = PortMaps.FirstOrDefault(m => m.SourceRoot == root)
                    ?? throw new InvalidOperationException("No subscriber for message type " + doc.DocumentElement.NamespaceURI + "#" + root);
                var canonical = portMap.Map == null ? doc : _lab.Map(portMap.Map).Execute(doc);
                var partnerId = message.Context.ReadVanArsdel(ContextProperties.PartnerId);
                if (string.IsNullOrEmpty(partnerId) || partnerId == "unknown")
                {
                    partnerId = Canonical(canonical, "PartnerId");
                }

                if (canonical.DocumentElement.LocalName == "FunctionalAck")
                {
                    // the 997/CONTRL maps leave PartnerId empty; the orchestration takes it from the EDI context
                    SetCanonical(canonical, "PartnerId", partnerId);
                    var ackNumber = Canonical(canonical, "AcknowledgedGroupNumber");
                    message.Context.PromoteVanArsdel(ContextProperties.AckControlNumber, string.IsNullOrEmpty(ackNumber) ? Canonical(canonical, "AcknowledgedControlNumber") : ackNumber);
                }

                partner = partner ?? _lab.Directory.Find(partnerId);
                var kind = portMap.Kind(partner, canonical);
                message.Context.PromoteVanArsdel(ContextProperties.PartnerId, partnerId);
                message.Context.PromoteVanArsdel(ContextProperties.MessageKind, kind);
                message.Context.Promote(ContextProperties.MessageType, ContextProperties.SystemNamespace, canonical.DocumentElement.NamespaceURI + "#" + canonical.DocumentElement.LocalName);
                var number = Canonical(canonical, "OrderNumber");
                if (!string.IsNullOrEmpty(number) && canonical.DocumentElement.LocalName == "Order")
                {
                    message.Context.PromoteVanArsdel(ContextProperties.OrderNumber, number);
                }

                if (root == "ORDERS05")
                {
                    message.Context.WriteVanArsdel(ContextProperties.SourceSystem, "ERP");
                }

                Published++;
                Orchestrations.Activate(canonical, message.Context);
            }
            catch (Exception ex)
            {
                _lab.Suspend("routing " + (doc.DocumentElement == null ? "message" : doc.DocumentElement.LocalName), ex, message.Body);
            }
        }

        /// <summary>The SFTP/FTP receive locations and the ERP file port: picks up every waiting file.</summary>
        public int PollFolders()
        {
            var count = 0;
            foreach (var partner in _lab.Directory.All.Where(p => p.Transport == TransportKind.Sftp || p.Transport == TransportKind.Ftp))
            {
                var folder = _lab.WorkPath(PartnerSimulator.FolderRoot(partner.Transport), partner.PartnerId, "inbound");
                if (!Directory.Exists(folder))
                {
                    continue;
                }

                foreach (var file in Directory.GetFiles(folder).Where(f => f.EndsWith(".x12", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".edi", StringComparison.OrdinalIgnoreCase)).OrderBy(f => f, StringComparer.Ordinal))
                {
                    var ctx = new MessageContext();
                    ctx.Promote(ContextProperties.ReceivedFileName, ContextProperties.FileNamespace, Path.GetFileName(file));
                    ctx.Promote(ContextProperties.InboundTransportLocation, ContextProperties.SystemNamespace, "sftp://" + partner.PartnerId.ToLowerInvariant() + "@sftp.vanarsdel.com/inbound/" + Path.GetFileName(file));
                    var body = File.ReadAllBytes(file);
                    File.Delete(file);
                    Receive(partner.IsSupplier ? "rl_Supplier_EDI_SFTP" : partner.Transport == TransportKind.Ftp ? "rl_Customer_EDI_FTP" : "rl_Customer_EDI_SFTP", body, ctx);
                    count++;
                }
            }

            foreach (var file in Directory.GetFiles(_lab.WorkPath("erp", "outbound"), "*.xml").OrderBy(f => f, StringComparer.Ordinal))
            {
                var ctx = new MessageContext();
                ctx.Promote(ContextProperties.ReceivedFileName, ContextProperties.FileNamespace, Path.GetFileName(file));
                var body = File.ReadAllBytes(file);
                File.Delete(file);
                Receive("rl_Erp_IDoc_FILE", body, ctx);
                count++;
            }

            return count;
        }

        /// <summary>AS2 receive (BizTalk's BTSHTTPReceive.dll endpoint): pipeline first, then the MDN.</summary>
        public string ReceiveAs2(string as2From, string as2To, string messageId, byte[] body)
        {
            var ctx = new MessageContext();
            ctx.Promote(ContextProperties.As2From, ContextProperties.As2Namespace, As2.Unquote(as2From));
            ctx.Promote(ContextProperties.As2To, ContextProperties.As2Namespace, As2.Unquote(as2To));
            ctx.Promote(ContextProperties.As2MessageId, ContextProperties.As2Namespace, messageId);
            var result = Receive("rl_Customer_EDI_AS2", body, ctx);
            return As2.Mdn(messageId, As2.Unquote(as2From), As2.OurAs2Id, body, result == null ? "unexpected-processing-error" : null);
        }

        public static string Canonical(XmlDocument doc, string headerElement)
        {
            var node = doc.SelectSingleNode("/*/*[local-name()='Header']/*[local-name()='" + headerElement + "'] | /*/*[local-name()='" + headerElement + "']");
            return node == null ? null : node.InnerText;
        }

        private static void SetCanonical(XmlDocument doc, string element, string value)
        {
            var node = doc.SelectSingleNode("/*/*[local-name()='" + element + "']");
            if (node != null)
            {
                node.InnerText = value ?? string.Empty;
            }
        }

        private static string AckKind(TradingPartner partner, XmlDocument doc)
        {
            return partner != null && partner.IsSupplier ? "PurchaseOrderAck" : "FunctionalAck";
        }
    }
}
