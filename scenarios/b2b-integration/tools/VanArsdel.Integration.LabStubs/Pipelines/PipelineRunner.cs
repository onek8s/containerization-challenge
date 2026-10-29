using System.Globalization;
using System.Text;
using System.Xml;
using VanArsdel.Integration.Contracts;
using VanArsdel.Integration.Domain.Orders;
using VanArsdel.Integration.Domain.Partners;
using VanArsdel.Integration.Edi;
using VanArsdel.Integration.Edi.Acks;
using VanArsdel.Integration.Edi.Envelopes;
using VanArsdel.Integration.LabStubs.Lab;
using VanArsdel.Integration.Messaging;

namespace VanArsdel.Integration.LabStubs.Pipelines
{
    public class ReceiveResult
    {
        public List<PipelineMessage> Messages { get; } = new List<PipelineMessage>();
        public List<XmlDocument> Acks { get; } = new List<XmlDocument>();
        public DisassemblyResult Interchange { get; set; }
        public TradingPartner Partner { get; set; }
        public List<string> Steps { get; } = new List<string>();
    }

    public class SendResult
    {
        public byte[] Body { get; set; }
        public string ControlNumber { get; set; }
        public string GroupControlNumber { get; set; }
        public string FileName { get; set; }
        public List<string> Steps { get; } = new List<string>();
    }

    public class PipelineException : Exception
    {
        public PipelineException(string component, Exception inner) : base(component + ": " + inner.Message, inner)
        {
        }
    }

    /// <summary>
    /// Runs a .btp pipeline with the portable implementations behind each component: the VanArsdel components'
    /// logic lives in VanArsdel.Integration.Messaging, the Microsoft EDI disassembler/assembler are stood in for by
    /// VanArsdel.Integration.Edi (same EDI XML shape). AS2 decode/encode and party resolution are no-ops here.
    /// </summary>
    public class PipelineRunner
    {
        public const string OurX12Qualifier = "ZZ";
        public const string OurX12Id = "VANARSDEL";
        public const string OurGln = "0614141000012";

        private readonly LabContext _lab;
        private readonly Dictionary<string, PipelineDefinition> _definitions = new Dictionary<string, PipelineDefinition>(StringComparer.OrdinalIgnoreCase);

        public PipelineRunner(LabContext lab)
        {
            _lab = lab;
        }

        public PipelineDefinition Definition(string name)
        {
            if (!_definitions.TryGetValue(name, out var d))
            {
                d = PipelineDefinition.Load(Path.Combine(ScenarioPaths.Pipelines, name + ".btp"));
                _definitions[name] = d;
            }

            return d;
        }

        public ReceiveResult Receive(string pipeline, byte[] body, IMessageContext context)
        {
            var definition = Definition(pipeline);
            var result = new ReceiveResult();
            var messages = new List<PipelineMessage> { new PipelineMessage(body, context) };

            foreach (var component in definition.Components)
            {
                try
                {
                    messages = Apply(component, messages, result);
                }
                catch (Exception ex) when (ex is not PipelineException)
                {
                    throw new PipelineException(component.DisplayName ?? component.ShortName, ex);
                }
            }

            result.Messages.AddRange(messages);
            return result;
        }

        private List<PipelineMessage> Apply(PipelineComponentDefinition c, List<PipelineMessage> input, ReceiveResult result)
        {
            var output = new List<PipelineMessage>();
            foreach (var m in input)
            {
                switch (c.ShortName)
                {
                    case "AS2Decoder":
                    case "PartyRes":
                        output.Add(m);
                        break;

                    case "EncodingFixerComponent":
                        var fixedBody = EncodingFixer.Fix(m.Body, new EncodingFixerSettings
                        {
                            FallbackCodePage = c.GetInt("FallbackCodePage", 1252),
                            RemoveControlCharacters = c.GetBool("RemoveControlCharacters", true),
                            UnwrapFixedLines = c.GetBool("UnwrapFixedLines", false),
                            TransliterateForEdi = c.GetBool("TransliterateForEdi", false)
                        });
                        m.Body = fixedBody.Body;
                        result.Steps.Add("encoding: " + EncodingFixer.Describe(fixedBody));
                        output.Add(m);
                        break;

                    case "PartnerResolverComponent":
                        var partner = PartnerResolution.Resolve(m.Context, _lab.Directory);
                        if (partner == null)
                        {
                            if (result.Interchange == null && !c.GetBool("FailWhenUnknown", true))
                            {
                                output.Add(m);
                                break;
                            }

                            // before the EDI disassembler only the transport can identify the sender; after it the envelope must
                            if (result.Interchange != null || c.GetBool("FailWhenUnknown", true) && m.Context.ReadString(ContextProperties.Isa06, ContextProperties.EdiNamespace) != null)
                            {
                                throw new InvalidOperationException("Unknown sender (AS2 " + m.Context.ReadString(ContextProperties.As2From, ContextProperties.As2Namespace) + ", ISA06 "
                                    + m.Context.ReadString(ContextProperties.Isa06, ContextProperties.EdiNamespace) + ", UNB " + m.Context.ReadString(ContextProperties.UnbSenderId, ContextProperties.EdiNamespace) + ")");
                            }

                            output.Add(m);
                            break;
                        }

                        PartnerResolution.Promote(m.Context, partner);
                        if (result.Partner == null)
                        {
                            result.Steps.Add("partner: " + partner.PartnerId);
                        }

                        result.Partner = partner;
                        output.Add(m);
                        break;

                    case "DuplicateCheckComponent":
                        if (c.GetBool("Enabled", true))
                        {
                            var hash = ArchiveNaming.Hash(m.Body);
                            m.Context.WriteVanArsdel(ContextProperties.BodyHash, hash);
                            var detector = new DuplicateDetector(_lab.Duplicates, TimeSpan.FromHours(c.GetInt("WindowHours", 72)));
                            if (detector.IsDuplicate(m.Context.ReadVanArsdel(ContextProperties.PartnerId) ?? "unknown", null, hash, _lab.UtcNow))
                            {
                                throw new InvalidOperationException("Duplicate interchange (hash " + hash + ")");
                            }
                        }

                        output.Add(m);
                        break;

                    case "ArchiveComponent":
                        if (c.GetBool("Enabled", true))
                        {
                            var probe = MessageTypeProbe.Probe(m.Body);
                            var relative = ArchiveNaming.Expand(c.Get("FileNamePattern", ArchiveNaming.DefaultPattern),
                                m.Context.ReadVanArsdel(ContextProperties.PartnerId) ?? "unknown", m.Context.ReadVanArsdel(ContextProperties.MessageKind) ?? "message",
                                m.MessageId, m.Context.ReadString(ContextProperties.ReceivedFileName, ContextProperties.FileNamespace), _lab.UtcNow, ArchiveNaming.ExtensionFor(probe.Format));
                            var full = _lab.WorkPath("archive", c.Get("Direction", "inbound"), relative.Replace('\\', '/'));
                            Directory.CreateDirectory(Path.GetDirectoryName(full));
                            File.WriteAllBytes(full, m.Body);
                            m.Context.WriteVanArsdel(ContextProperties.ArchivePath, relative);
                        }

                        output.Add(m);
                        break;

                    case "EdiDisassembler":
                        output.AddRange(Disassemble(m, result));
                        break;

                    case "XmlDebatcherComponent":
                    case "IDocSplitterComponent":
                        using (var stream = new MemoryStream(m.Body))
                        {
                            var parts = XmlDebatcher.Split(stream, c.Get("BodyElement", "Order"), c.GetBool("WrapInRoot", false));
                            foreach (var part in parts)
                            {
                                var ctx = ((MessageContext)m.Context).Clone();
                                ctx.Promote(ContextProperties.MessageType, ContextProperties.SystemNamespace, part.MessageType);
                                if (part.EnvelopeAttributes != null && part.EnvelopeAttributes.TryGetValue("PartnerId", out var pid))
                                {
                                    ctx.PromoteVanArsdel(ContextProperties.PartnerId, pid);
                                }

                                output.Add(new PipelineMessage(part.Body, ctx));
                            }

                            result.Steps.Add("debatched " + parts.Count + " " + c.Get("BodyElement", "Order") + " message(s)");
                        }

                        break;

                    case "XmlValidator":
                        var issues = _lab.Schemas.Validate(m.Body).Where(i => i.Severity == System.Xml.Schema.XmlSeverityType.Error).ToList();
                        if (issues.Count > 0)
                        {
                            throw new InvalidOperationException("Schema validation failed: " + string.Join("; ", issues.Take(3)));
                        }

                        output.Add(m);
                        break;

                    case "ContextEnricherComponent":
                        if (!string.IsNullOrEmpty(c.Get("MessageKind")))
                        {
                            m.Context.PromoteVanArsdel(ContextProperties.MessageKind, c.Get("MessageKind"));
                        }

                        if (!string.IsNullOrEmpty(c.Get("SourceSystem")))
                        {
                            m.Context.WriteVanArsdel(ContextProperties.SourceSystem, c.Get("SourceSystem"));
                        }

                        var xpath = c.Get("OrderNumberXPath");
                        if (!string.IsNullOrEmpty(xpath))
                        {
                            var nav = new System.Xml.XPath.XPathDocument(new MemoryStream(m.Body)).CreateNavigator();
                            var value = nav.Evaluate("string(" + xpath + ")") as string;
                            if (!string.IsNullOrEmpty(value))
                            {
                                m.Context.PromoteVanArsdel(ContextProperties.OrderNumber, value);
                            }
                        }

                        output.Add(m);
                        break;

                    case "InterchangeTrackingComponent":
                        output.Add(m);
                        break;

                    default:
                        throw new NotSupportedException("The lab has no stand-in for pipeline component " + c.TypeName);
                }
            }

            return output;
        }

        private IEnumerable<PipelineMessage> Disassemble(PipelineMessage m, ReceiveResult result)
        {
            var text = Encoding.UTF8.GetString(m.Body);
            var dis = EdiDisassembler.Disassemble(text);
            result.Interchange = dis;
            var h = dis.Interchange.Header;
            if (dis.HasEnvelopeErrors)
            {
                throw new InvalidOperationException("Interchange envelope errors: " + string.Join("; ", dis.Interchange.EnvelopeErrors));
            }

            if (h.Standard == EdiStandard.X12)
            {
                m.Context.Promote(ContextProperties.Isa06, ContextProperties.EdiNamespace, h.SenderId);
                m.Context.Promote("ISA05", ContextProperties.EdiNamespace, h.SenderQualifier);
                m.Context.Promote(ContextProperties.Isa13, ContextProperties.EdiNamespace, h.ControlNumber);
            }
            else
            {
                m.Context.Promote(ContextProperties.UnbSenderId, ContextProperties.EdiNamespace, h.SenderId);
                m.Context.Promote("UNB2_2", ContextProperties.EdiNamespace, h.SenderQualifier);
                m.Context.Promote(ContextProperties.UnbReference, ContextProperties.EdiNamespace, h.ControlNumber);
            }

            m.Context.PromoteVanArsdel(ContextProperties.InterchangeControlNumber, h.ControlNumber);
            var partner = PartnerResolution.Resolve(m.Context, _lab.Directory);
            // no acknowledgement for acknowledgements (997, CONTRL)
            if (partner != null && (partner.AckRequired || h.AckRequested) && dis.Transactions.Any(t => t.Set.Type != "997" && t.Set.Type != "CONTRL"))
            {
                result.Acks.AddRange(AckBuilder.Build(dis));
            }

            result.Steps.Add(string.Format(CultureInfo.InvariantCulture, "EDI {0} interchange {1}: {2} set(s), {3} rejected", h.Standard, h.ControlNumber, dis.Transactions.Count, dis.Rejected.Count()));
            foreach (var t in dis.Accepted)
            {
                var ctx = ((MessageContext)m.Context).Clone();
                var root = t.Document.DocumentElement;
                ctx.Promote(ContextProperties.MessageType, ContextProperties.SystemNamespace, root.NamespaceURI + "#" + root.LocalName);
                yield return new PipelineMessage(Encoding.UTF8.GetBytes(t.Document.OuterXml), ctx);
            }

            foreach (var t in dis.Rejected)
            {
                _lab.Trace("set {0} {1} rejected by EDI validation: {2}", t.Set.Type, t.Set.ControlNumber, string.Join("; ", t.Set.Errors.Take(3)));
            }
        }

        /// <summary>EDI send pipeline: envelope the documents for the partner and archive.</summary>
        /// <param name="controlOverride">EDIOverride.ISA13/UNB5 set by the orchestration (GS06 follows for X12).</param>
        /// <param name="ackRequested">Overrides the agreement's ack request, e.g. no ISA14=1 on an acknowledgement.</param>
        public SendResult SendEdi(string pipeline, IList<XmlDocument> documents, TradingPartner partner, string kind, string controlOverride = null, bool? ackRequested = null)
        {
            var definition = Definition(pipeline);
            var result = new SendResult();
            var isX12 = partner.Standard == DocumentStandard.X12;
            string control;
            string group;
            if (controlOverride != null)
            {
                control = controlOverride;
                group = isX12 ? long.Parse(controlOverride, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture) : "1";
            }
            else
            {
                control = _lab.ControlNumbers.Next(partner.PartnerId, "Interchange").ToString(isX12 ? "000000000" : "0000000", CultureInfo.InvariantCulture);
                group = _lab.ControlNumbers.Next(partner.PartnerId, "GS").ToString(CultureInfo.InvariantCulture);
                control = isX12 ? control : "VA" + control;
            }

            var header = new InterchangeHeader
            {
                Standard = isX12 ? EdiStandard.X12 : EdiStandard.Edifact,
                SenderQualifier = isX12 ? OurX12Qualifier : "14",
                SenderId = isX12 ? OurX12Id : OurGln,
                ReceiverQualifier = partner.InterchangeQualifier,
                ReceiverId = partner.InterchangeId,
                ControlNumber = control,
                Prepared = _lab.Now,
                Version = partner.Version,
                AckRequested = ackRequested ?? partner.AckRequired,
                Test = partner.TestIndicator
            };

            foreach (var component in definition.Components)
            {
                switch (component.ShortName)
                {
                    case "EdiAssembler":
                        var text = new InterchangeBuilder(header).Build(documents, group);
                        result.Body = new UTF8Encoding(false).GetBytes(text);
                        result.ControlNumber = header.ControlNumber;
                        result.GroupControlNumber = group;
                        result.Steps.Add("EDI assembled, interchange " + header.ControlNumber);
                        break;
                    case "ArchiveComponent":
                        result.FileName = partner.PartnerId + "_" + kind + "_" + header.ControlNumber + (isX12 ? ".x12" : ".edi");
                        var full = _lab.WorkPath("archive", "outbound", partner.PartnerId, result.FileName);
                        Directory.CreateDirectory(Path.GetDirectoryName(full));
                        File.WriteAllBytes(full, result.Body);
                        break;
                    case "InterchangeTrackingComponent":
                    case "AS2Encoder":
                        break;
                    default:
                        throw new NotSupportedException("The lab has no stand-in for send component " + component.TypeName);
                }
            }

            return result;
        }

        /// <summary>IDoc send pipeline (XML assembler, UTF-8 with declaration) to the ERP's inbound folder.</summary>
        public string SendIDoc(XmlDocument idoc, string partnerId, string kind, string reference)
        {
            var definition = Definition("IDocSend");
            if (!definition.Components.Any(c => c.ShortName == "XmlAsmComp"))
            {
                throw new InvalidOperationException("IDocSend has no XML assembler");
            }

            var file = _lab.WorkPath("erp", "inbound", kind + "_" + partnerId + "_" + reference + "_" + Guid.NewGuid().ToString("N")[..6] + ".xml");
            using (var writer = XmlWriter.Create(file, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true }))
            {
                idoc.Save(writer);
            }

            var archive = _lab.WorkPath("archive", "outbound", partnerId, Path.GetFileName(file));
            Directory.CreateDirectory(Path.GetDirectoryName(archive));
            File.Copy(file, archive, true);
            return file;
        }
    }
}
