using System.Globalization;
using System.Text;
using System.Xml;
using VanArsdel.Integration.Domain.Partners;
using VanArsdel.Integration.Edi;
using VanArsdel.Integration.Edi.Acks;
using VanArsdel.Integration.Edi.Envelopes;
using VanArsdel.Integration.LabStubs.Lab;
using VanArsdel.Integration.LabStubs.Pipelines;

namespace VanArsdel.Integration.LabStubs.Simulators
{
    public class PartnerDocument
    {
        public string PartnerId { get; set; }
        public string Transport { get; set; }
        public string ControlNumber { get; set; }
        public List<string> TransactionTypes { get; } = new List<string>();
        public string File { get; set; }

        public override string ToString()
        {
            return PartnerId + " got " + string.Join("+", TransactionTypes) + " interchange " + ControlNumber + " via " + Transport;
        }
    }

    /// <summary>
    /// The trading partners' side: their AS2 stations and the SFTP/FTP accounts we deliver to. Received interchanges
    /// are parsed with the EDI library (so a broken envelope shows up here), stored in partners/&lt;id&gt;/ and
    /// acknowledged with a 997 or CONTRL when the interchange asks for one. Partners also submit documents to us.
    /// </summary>
    public class PartnerSimulator
    {
        private readonly LabContext _lab;
        private readonly HttpClient _http;
        private readonly Func<string> _baseUrl;
        private readonly Queue<(TradingPartner Partner, IList<XmlDocument> Acks)> _pendingAcks = new Queue<(TradingPartner, IList<XmlDocument>)>();
        private readonly object _gate = new object();
        private long _nextControl = 5001;

        public PartnerSimulator(LabContext lab, HttpClient http, Func<string> baseUrl)
        {
            _lab = lab;
            _http = http;
            _baseUrl = baseUrl;
        }

        public List<PartnerDocument> Received { get; } = new List<PartnerDocument>();

        /// <summary>Partners whose translator does not return functional acknowledgements.</summary>
        public HashSet<string> WithoutAcks { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static string FolderRoot(TransportKind transport)
        {
            return transport == TransportKind.Ftp ? "ftp" : "sftp";
        }

        /// <summary>AS2 station of a partner (POST /partners/{as2id}/as2). Returns the synchronous MDN.</summary>
        public string HandleAs2(string as2To, string as2From, string messageId, byte[] body)
        {
            var partner = _lab.Directory.All.FirstOrDefault(p => string.Equals(p.As2Identifier, As2.Unquote(as2To), StringComparison.OrdinalIgnoreCase));
            if (partner == null)
            {
                return As2.Mdn(messageId, as2From, as2To, body, "unknown-trading-partner");
            }

            try
            {
                Accept(partner, body, "AS2", messageId);
                return As2.Mdn(messageId, As2.Unquote(as2From), partner.As2Identifier, body, null);
            }
            catch (Exception ex)
            {
                return As2.Mdn(messageId, As2.Unquote(as2From), partner.As2Identifier, body, "decryption-failed: " + ex.Message);
            }
        }

        /// <summary>Partners collect what the SFTP/FTP send ports left in their outbound folders.</summary>
        public int CollectFolders()
        {
            var count = 0;
            foreach (var partner in _lab.Directory.All.Where(p => p.Transport == TransportKind.Sftp || p.Transport == TransportKind.Ftp))
            {
                var folder = _lab.WorkPath(FolderRoot(partner.Transport), partner.PartnerId, "outbound");
                if (!Directory.Exists(folder))
                {
                    continue;
                }

                foreach (var file in Directory.GetFiles(folder).Where(f => !f.EndsWith(".tmp", StringComparison.Ordinal)).OrderBy(f => f, StringComparer.Ordinal))
                {
                    Accept(partner, File.ReadAllBytes(file), partner.Transport.ToString().ToUpperInvariant(), Path.GetFileName(file));
                    File.Delete(file);
                    count++;
                }
            }

            return count;
        }

        private void Accept(TradingPartner partner, byte[] body, string transport, string reference)
        {
            var dis = EdiDisassembler.Disassemble(Encoding.UTF8.GetString(body));
            if (dis.HasEnvelopeErrors)
            {
                throw new InvalidOperationException(string.Join("; ", dis.Interchange.EnvelopeErrors));
            }

            var h = dis.Interchange.Header;
            var ours = h.Standard == EdiStandard.X12 ? h.SenderId == PipelineRunner.OurX12Id : h.SenderId == PipelineRunner.OurGln;
            if (!ours || !partner.MatchesInterchange(h.ReceiverQualifier, h.ReceiverId))
            {
                throw new InvalidOperationException("Interchange " + h.ControlNumber + " is not addressed from VanArsdel to " + partner.PartnerId);
            }

            var doc = new PartnerDocument { PartnerId = partner.PartnerId, Transport = transport, ControlNumber = h.ControlNumber };
            doc.TransactionTypes.AddRange(dis.Transactions.Select(t => t.Set.Type));
            var inbox = Directory.CreateDirectory(_lab.WorkPath("partners", partner.PartnerId, "inbox")).FullName;
            doc.File = Path.Combine(inbox, h.ControlNumber + "_" + string.Join("-", doc.TransactionTypes) + (h.Standard == EdiStandard.X12 ? ".x12" : ".edi"));
            File.WriteAllBytes(doc.File, body);

            var rejected = dis.Rejected.ToList();
            lock (_gate)
            {
                Received.Add(doc);
                var isAck = doc.TransactionTypes.All(t => t == "997" || t == "CONTRL");
                if (h.AckRequested && !isAck && !WithoutAcks.Contains(partner.PartnerId))
                {
                    _pendingAcks.Enqueue((partner, AckBuilder.Build(dis)));
                }
            }

            _lab.Trace("{0} partner {1}: received {2} interchange {3} ({4} set(s){5})", transport, partner.PartnerId, string.Join("+", doc.TransactionTypes), h.ControlNumber,
                dis.Transactions.Count, rejected.Count == 0 ? string.Empty : ", " + rejected.Count + " failed partner validation: " + string.Join("; ", rejected.SelectMany(r => r.Set.Errors).Take(2)));
        }

        /// <summary>Sends the queued 997/CONTRL acknowledgements back to us, each in its own interchange.</summary>
        public int SendAcknowledgements()
        {
            var sent = 0;
            while (true)
            {
                (TradingPartner Partner, IList<XmlDocument> Acks) next;
                lock (_gate)
                {
                    if (_pendingAcks.Count == 0)
                    {
                        break;
                    }

                    next = _pendingAcks.Dequeue();
                }

                var text = Envelope(next.Partner, next.Acks);
                Submit(next.Partner, Encoding.UTF8.GetBytes(text), next.Partner.PartnerId + "_ACK_" + _nextControl + ".edi");
                sent++;
            }

            return sent;
        }

        /// <summary>Wraps documents in an interchange from the partner to VanArsdel.</summary>
        public string Envelope(TradingPartner partner, IEnumerable<XmlDocument> documents)
        {
            long control;
            lock (_gate)
            {
                control = _nextControl++;
            }

            var x12 = partner.Standard == DocumentStandard.X12;
            var header = new InterchangeHeader
            {
                Standard = x12 ? EdiStandard.X12 : EdiStandard.Edifact,
                SenderQualifier = partner.InterchangeQualifier,
                SenderId = partner.InterchangeId,
                ReceiverQualifier = x12 ? PipelineRunner.OurX12Qualifier : "14",
                ReceiverId = x12 ? PipelineRunner.OurX12Id : PipelineRunner.OurGln,
                ControlNumber = x12 ? control.ToString("000000000", CultureInfo.InvariantCulture) : partner.PartnerId + control.ToString("0000000", CultureInfo.InvariantCulture),
                Prepared = _lab.Now,
                Version = partner.Version,
                AckRequested = false,
                Test = partner.TestIndicator
            };

            return new InterchangeBuilder(header).Build(documents, control.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>A partner sends us a document over its own transport.</summary>
        public string Submit(TradingPartner partner, byte[] body, string fileName)
        {
            switch (partner.Transport)
            {
                case TransportKind.As2:
                    try
                    {
                        var receipt = As2.Post(_http, _baseUrl() + "/b2b/as2", partner.As2Identifier, As2.OurAs2Id, body, fileName);
                        _lab.Trace("{0} sent {1} over AS2: {2}", partner.PartnerId, fileName, As2.Describe(receipt));
                        return receipt.MessageId;
                    }
                    catch (InvalidOperationException ex)
                    {
                        // a negative MDN: the partner's AS2 station logs it and its operator calls our support desk
                        _lab.Trace("{0} sent {1} over AS2: negative receipt, {2}", partner.PartnerId, fileName, ex.Message);
                        return null;
                    }
                case TransportKind.Sftp:
                case TransportKind.Ftp:
                    var folder = Directory.CreateDirectory(_lab.WorkPath(FolderRoot(partner.Transport), partner.PartnerId, "inbound")).FullName;
                    var target = Path.Combine(folder, fileName);
                    File.WriteAllBytes(target + ".part", body);
                    File.Move(target + ".part", target, true);
                    _lab.Trace("{0} uploaded {1} to its {2} inbound folder", partner.PartnerId, fileName, partner.Transport.ToString().ToUpperInvariant());
                    return target;
                default:
                    throw new NotSupportedException(partner.PartnerId + " does not send EDI (" + partner.Transport + ")");
            }
        }
    }
}
