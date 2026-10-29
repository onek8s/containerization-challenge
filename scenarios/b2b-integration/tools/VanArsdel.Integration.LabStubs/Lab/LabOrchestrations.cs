using System.Globalization;
using System.Text;
using System.Xml;
using VanArsdel.Integration.Contracts;
using VanArsdel.Integration.Contracts.Canonical;
using VanArsdel.Integration.Contracts.Erp;
using VanArsdel.Integration.Domain.Orders;
using VanArsdel.Integration.Domain.Partners;
using VanArsdel.Integration.Domain.Routing;
using VanArsdel.Integration.Messaging;
using VanArsdel.Integration.Rules;

namespace VanArsdel.Integration.LabStubs.Lab
{
    public class OrchestrationRun
    {
        public string Orchestration { get; set; }
        public string PartnerId { get; set; }
        public string Document { get; set; }
        public string Result { get; set; }

        public override string ToString()
        {
            return Orchestration + " " + PartnerId + " " + Document + ": " + Result;
        }
    }

    /// <summary>
    /// The orchestrations of VanArsdel.Integration.Orchestrations, shape by shape, with the lab's stand-ins for
    /// the BRE (BrlPolicy on the same .xml policies), BAM (LabContext.Bam), the integration database and the ports.
    /// The facts classes, maps and routing slip are the deployed ones.
    /// </summary>
    public class LabOrchestrations
    {
        public const int OrderCutOffHour = 15;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static readonly IReadOnlyDictionary<string, string> NotifyGroups = new Dictionary<string, string>
        {
            ["CustomerService"] = "customer.service@vanarsdel.com",
            ["Credit"] = "credit.control@vanarsdel.com",
            ["Integration"] = "integration.support@vanarsdel.com",
            ["Purchasing"] = "purchasing@vanarsdel.com"
        };

        private readonly LabContext _lab;
        private readonly LabPlatform _platform;
        private readonly Func<IOrderManagementService> _erp;
        private readonly List<PendingAck> _pendingAcks = new List<PendingAck>();

        public LabOrchestrations(LabContext lab, LabPlatform platform, Func<IOrderManagementService> erp)
        {
            _lab = lab;
            _platform = platform;
            _erp = erp;
            platform.Orchestrations = this;
        }

        public List<OrchestrationRun> Runs { get; } = new List<OrchestrationRun>();

        public IEnumerable<DateTime> Holidays { get; set; } = new[] { new DateTime(2026, 1, 1), new DateTime(2026, 5, 25), new DateTime(2026, 7, 3), new DateTime(2026, 12, 25) };

        public int PendingPurchaseOrderAcks
        {
            get
            {
                lock (_pendingAcks)
                {
                    return _pendingAcks.Count;
                }
            }
        }

        /// <summary>Message box: the activating receive whose filter matches the message.</summary>
        public void Activate(XmlDocument message, IMessageContext context)
        {
            var kind = context.ReadVanArsdel(ContextProperties.MessageKind);
            var type = context.MessageType();
            if (type == MessageTypes.Order && kind == "CustomerOrder")
            {
                ProcessCustomerOrder(message, context);
            }
            else if (type == MessageTypes.Despatch)
            {
                SendCustomerDocument(message, context);
            }
            else if (type == MessageTypes.Invoice && kind == "CustomerInvoice")
            {
                SendCustomerInvoice(message, context);
            }
            else if (type == MessageTypes.Invoice && (kind == "SupplierInvoice" || kind == "SupplierCreditNote"))
            {
                ProcessSupplierInvoice(message, context);
            }
            else if (type == MessageTypes.FunctionalAck && kind != "PurchaseOrderAck")
            {
                ProcessFunctionalAck(message, context);
            }
            else if (type == MessageTypes.FunctionalAck)
            {
                CorrelatePurchaseOrderAck(message, context);
            }
            else if (kind == "ORDERS" && context.ReadVanArsdel(ContextProperties.SourceSystem) == "ERP")
            {
                SupplierPurchaseOrder(message);
            }
            else
            {
                throw new InvalidOperationException("Routing failure: no subscription matches MessageType " + type + ", MessageKind " + kind);
            }
        }

        public void ProcessCustomerOrder(XmlDocument orderMsg, IMessageContext context)
        {
            var partnerId = context.ReadVanArsdel(ContextProperties.PartnerId);
            var documentNumber = context.ReadVanArsdel(ContextProperties.OrderNumber);
            var order = CanonicalSerializer.Deserialize<Order>(orderMsg);
            var partner = _lab.Directory.Get(partnerId);
            var row = _lab.Track(partnerId, documentNumber);
            row.ReceivedUtc = _lab.UtcNow;
            row.LineCount = order.Lines?.Length ?? 0;

            try
            {
                var outcome = new RuleOutcome();
                Execute("VanArsdel.OrderValidation", outcome, new OrderFacts(order), new PartnerFacts(partner), new CalendarFacts(_lab.Now, OrderCutOffHour, Holidays));
                if (outcome.Rejected || outcome.ManualReview)
                {
                    row.RejectedUtc = _lab.UtcNow;
                    row.RejectReason = outcome.Summary();
                    _lab.Mail(NotifyGroups["CustomerService"], Subject("Rejected order", partnerId, documentNumber), RejectionNotice(partner, documentNumber, outcome));
                    Done("ProcessCustomerOrder", partnerId, documentNumber, "terminated: " + outcome.Summary());
                    return;
                }

                row.ValidatedUtc = _lab.UtcNow;
                var released = !outcome.CreditCheckRequired;
                if (outcome.CreditCheckRequired)
                {
                    var request = new CreditCheckRequest
                    {
                        SoldTo = order.Header.ErpPartnerNumber,
                        OrderValue = decimal.Parse(new OrderFacts(order).GetOrderValue().ToString("0.00", Inv), Inv),
                        Currency = order.Header.Currency,
                        Reference = order.Header.OrderNumber
                    };
                    var response = _erp().CheckCredit(request);
                    _lab.Trace("ERP.OrderManagement CheckCredit {0} {1:0.00} {2}: {3}", request.SoldTo, request.OrderValue, request.Currency, Simulators.ErpOrderService.Describe(response));
                    var partnerFacts = new PartnerFacts(partner)
                    {
                        CreditChecked = true,
                        CreditApproved = response.Approved,
                        CreditLimit = response.CreditLimit,
                        OpenExposure = response.OpenExposure,
                        BlockReason = response.BlockReason
                    };
                    Execute("VanArsdel.CreditRelease", outcome, new OrderFacts(order), partnerFacts);
                    released = !outcome.CreditHold;
                    if (outcome.CreditHold)
                    {
                        row.CreditHoldUtc = _lab.UtcNow;
                        row.HoldReason = outcome.HoldReason;
                        var hold = _lab.WorkPath("credithold", partnerId + "_" + documentNumber + ".xml");
                        Directory.CreateDirectory(Path.GetDirectoryName(hold));
                        orderMsg.Save(hold);
                        _lab.Mail(NotifyGroups["Credit"], Subject("Credit hold", partnerId, documentNumber), "Order " + documentNumber + " is on credit hold: " + outcome.HoldReason);
                    }
                }

                if (released)
                {
                    ApplyOutcome(order, outcome);
                    var erpOrder = CanonicalToXml(order);
                    var idoc = _lab.Map("Order_to_IDoc_ORDERS05").Execute(erpOrder);
                    _platform.Pipelines.SendIDoc(idoc, partnerId, "ORDERS05", Safe(documentNumber));
                    row.ReleasedUtc = _lab.UtcNow;
                    row.SentToErpUtc = _lab.UtcNow;
                    row.OrderValue = new OrderFacts(order).GetOrderValue();
                }

                Done("ProcessCustomerOrder", partnerId, documentNumber, released ? "sent to ERP (" + string.Join(", ", outcome.FiredRules) + ")" : "credit hold: " + outcome.HoldReason);
            }
            catch (Exception ex)
            {
                _lab.Mail(NotifyGroups["Integration"], Subject("Order failed", partnerId, documentNumber), "Order " + documentNumber + " failed: " + ex.Message);
                Done("ProcessCustomerOrder", partnerId, documentNumber, "terminated: " + ex.Message);
            }
        }

        public void SendCustomerDocument(XmlDocument despatchMsg, IMessageContext context)
        {
            var partnerId = context.ReadVanArsdel(ContextProperties.PartnerId);
            var documentNumber = LabPlatform.Canonical(despatchMsg, "DespatchNumber");
            var erpOrderNumber = LabPlatform.Canonical(despatchMsg, "ErpOrderNumber");
            var row = Confirmed(partnerId, LabPlatform.Canonical(despatchMsg, "OrderNumber"), erpOrderNumber);
            if (row != null)
            {
                row.DespatchNumber = documentNumber;
                row.DespatchedUtc = _lab.UtcNow;
            }

            var slip = RoutingSlip.Parse(RoutingSlipFor(partnerId, "Despatch"));
            var partnerMsg = despatchMsg;
            var steps = new List<string>();
            while (!slip.IsComplete)
            {
                switch (slip.Current.Kind)
                {
                    case RoutingStepKind.Transform:
                        partnerMsg = _lab.Map(slip.Current.Target).Execute(despatchMsg);
                        break;
                    case RoutingStepKind.Send:
                        Send(slip.Current.Target, partnerId, partnerMsg, DocumentKind.Despatch, null);
                        if (row != null)
                        {
                            row.AsnSentUtc = _lab.UtcNow;
                        }

                        break;
                    default:
                        _lab.Trace("routing step {0} handled by the send port", slip.Current);
                        break;
                }

                steps.Add(slip.Current.ToString());
                slip = slip.Advance();
            }

            Done("SendCustomerDocument", partnerId, documentNumber, string.Join(" > ", steps));
        }

        public void SendCustomerInvoice(XmlDocument invoiceMsg, IMessageContext context)
        {
            var partnerId = context.ReadVanArsdel(ContextProperties.PartnerId);
            var documentNumber = LabPlatform.Canonical(invoiceMsg, "InvoiceNumber");
            var erpOrderNumber = LabPlatform.Canonical(invoiceMsg, "ErpOrderNumber");
            var partner = _lab.Directory.Get(partnerId);
            var map = partner.Standard == DocumentStandard.X12 ? "Invoice_to_X12_00401_810" : "Invoice_to_EFACT_D96A_INVOIC";
            var partnerMsg = _lab.Map(map).Execute(invoiceMsg);
            var endpoint = EndpointFor(partnerId, "Invoice");
            Send(endpoint, partnerId, partnerMsg, DocumentKind.CustomerInvoice, null);
            var row = _lab.Bam.Values.FirstOrDefault(r => r.ErpOrderNumber == erpOrderNumber);
            if (row != null)
            {
                row.InvoiceNumber = documentNumber;
                row.InvoicedUtc = _lab.UtcNow;
            }

            var total = invoiceMsg.SelectSingleNode("/*/*[local-name()='Summary']/*[local-name()='GrandTotal']")?.InnerText;
            Done("SendCustomerInvoice", partnerId, documentNumber, map + " > " + endpoint + " (" + total + " " + LabPlatform.Canonical(invoiceMsg, "Currency") + ")");
        }

        public void ProcessFunctionalAck(XmlDocument ackMsg, IMessageContext context)
        {
            var partnerId = context.ReadVanArsdel(ContextProperties.PartnerId);
            var documentNumber = context.ReadVanArsdel(ContextProperties.AckControlNumber);
            var status = ackMsg.SelectSingleNode("/*/*[local-name()='Status']")?.InnerText;
            var matched = ApplyAck(partnerId, documentNumber, status);
            if (status == "Rejected" || status == "AcceptedWithErrors" || !matched)
            {
                _lab.Mail(NotifyGroups["Integration"], Subject("Acknowledgement problem", partnerId, documentNumber),
                    "Partner " + partnerId + " acknowledged control number " + documentNumber + " with " + status + (matched ? string.Empty : " (no matching interchange)"));
            }

            Done("ProcessFunctionalAck", partnerId, documentNumber, status + (matched ? string.Empty : ", no matching interchange"));
        }

        public void ProcessSupplierInvoice(XmlDocument invoiceMsg, IMessageContext context)
        {
            var partnerId = context.ReadVanArsdel(ContextProperties.PartnerId);
            var documentNumber = LabPlatform.Canonical(invoiceMsg, "InvoiceNumber");
            var unresolved = invoiceMsg.SelectSingleNode("/*/*[local-name()='Lines']/*[starts-with(*[local-name()='Item'], '?')][1]/*[local-name()='PartnerItem']")?.InnerText ?? string.Empty;
            if (unresolved != string.Empty)
            {
                _lab.Mail(NotifyGroups["Purchasing"], Subject("Supplier invoice item", partnerId, documentNumber),
                    "Supplier invoice " + documentNumber + " from " + partnerId + " has an item without cross-reference: " + unresolved);
            }

            var idoc = _lab.Map("Invoice_to_IDoc_INVOIC02").Execute(invoiceMsg);
            _platform.Pipelines.SendIDoc(idoc, partnerId, "INVOIC02", Safe(documentNumber));
            Done("ProcessSupplierInvoice", partnerId, documentNumber, "INVOIC02 to ERP" + (unresolved == string.Empty ? string.Empty : ", unresolved item " + unresolved));
        }

        public void SupplierPurchaseOrder(XmlDocument poIDocMsg)
        {
            var poMsg = _lab.Map("IDoc_ORDERS05_to_Order").Execute(poIDocMsg);
            var partnerId = LabPlatform.Canonical(poMsg, "PartnerId");
            var documentNumber = LabPlatform.Canonical(poMsg, "OrderNumber");
            var partner = _lab.Directory.Get(partnerId);
            var controlNumber = _lab.ControlNumbers.Next(partnerId, "Interchange").ToString("D9", Inv);
            var ackTimeout = TimeSpan.FromMinutes(partner.AckTimeoutMinutes > 0 ? partner.AckTimeoutMinutes : 1440);
            var edifact = partner.Standard == DocumentStandard.Edifact;
            var supplierMsg = _lab.Map(edifact ? "Order_to_EFACT_D96A_ORDERS" : "Order_to_X12_00401_850").Execute(poMsg);
            var ackControlNumber = edifact ? controlNumber : long.Parse(controlNumber, Inv).ToString(Inv);
            Send(EndpointFor(partnerId, "PurchaseOrder"), partnerId, supplierMsg, DocumentKind.PurchaseOrder, controlNumber);
            lock (_pendingAcks)
            {
                _pendingAcks.Add(new PendingAck { PartnerId = partnerId, ControlNumber = ackControlNumber, DocumentNumber = documentNumber, DueLocal = _lab.Now.Add(ackTimeout), Timeout = ackTimeout });
            }

            Done("SupplierPurchaseOrder", partnerId, documentNumber, "sent, interchange " + controlNumber + ", waiting " + ackTimeout.TotalHours.ToString("0.#", Inv) + " h for the acknowledgement");
        }

        /// <summary>The listen shape of SupplierPurchaseOrder: the ack correlates on PartnerId + AckControlNumber.</summary>
        private void CorrelatePurchaseOrderAck(XmlDocument ackMsg, IMessageContext context)
        {
            var partnerId = context.ReadVanArsdel(ContextProperties.PartnerId);
            var controlNumber = context.ReadVanArsdel(ContextProperties.AckControlNumber);
            PendingAck waiting;
            lock (_pendingAcks)
            {
                waiting = _pendingAcks.FirstOrDefault(p => p.PartnerId == partnerId && p.ControlNumber == controlNumber);
                if (waiting == null)
                {
                    throw new InvalidOperationException("Routing failure: no SupplierPurchaseOrder instance waits for " + partnerId + " acknowledgement " + controlNumber);
                }

                _pendingAcks.Remove(waiting);
            }

            var status = ackMsg.SelectSingleNode("/*/*[local-name()='Status']")?.InnerText;
            ApplyAck(partnerId, controlNumber, status);
            Done("SupplierPurchaseOrder", partnerId, waiting.DocumentNumber, "acknowledged " + status);
        }

        /// <summary>Fires the timeout branch of every listen whose delay has passed (lab clock).</summary>
        public int CheckAckTimeouts()
        {
            List<PendingAck> expired;
            lock (_pendingAcks)
            {
                expired = _pendingAcks.Where(p => _lab.Now >= p.DueLocal).ToList();
                expired.ForEach(p => _pendingAcks.Remove(p));
            }

            foreach (var p in expired)
            {
                _lab.Mail(NotifyGroups["Purchasing"], Subject("Missing acknowledgement", p.PartnerId, p.DocumentNumber),
                    "No 997/CONTRL received for purchase order " + p.DocumentNumber + " (control number " + p.ControlNumber + ") within " + p.Timeout.ToString());
                Done("SupplierPurchaseOrder", p.PartnerId, p.DocumentNumber, "acknowledgement timed out");
            }

            return expired.Count;
        }

        /// <summary>EDI receive with ack generation: the 997/CONTRL goes back through the partner's send port.</summary>
        public void SendGeneratedAck(TradingPartner partner, XmlDocument ack)
        {
            if (partner == null)
            {
                return;
            }

            try
            {
                var endpoint = partner.Transport.ToString().ToUpperInvariant() + "." + partner.PartnerId;
                var sent = _platform.Pipelines.SendEdi(PipelineFor(endpoint), new[] { ack }, partner, "FunctionalAck", null, false);
                _lab.RegisterInterchange(partner.PartnerId, InterchangeDirection.Outbound, partner.Standard == DocumentStandard.X12 ? "997" : "CONTRL", sent.ControlNumber, sent.GroupControlNumber, 1, sent.FileName, null);
                _platform.Transport.Deliver(endpoint, partner, sent.Body, sent.FileName);
            }
            catch (Exception ex)
            {
                _lab.Suspend("ack to " + partner.PartnerId, ex, Encoding.UTF8.GetBytes(ack.OuterXml));
            }
        }

        private void Send(string endpoint, string partnerId, XmlDocument partnerMsg, DocumentKind kind, string controlOverride)
        {
            var partner = _lab.Directory.Get(partnerId);
            var sent = _platform.Pipelines.SendEdi(PipelineFor(endpoint), new[] { partnerMsg }, partner, kind.ToString(), controlOverride);
            var window = partner.AckRequired ? TimeSpan.FromMinutes(partner.AckTimeoutMinutes > 0 ? partner.AckTimeoutMinutes : 1440) : (TimeSpan?)null;
            _lab.RegisterInterchange(partnerId, InterchangeDirection.Outbound, DocumentKinds.ToTransactionType(kind, partner.Standard), sent.ControlNumber, sent.GroupControlNumber, 1, sent.FileName, window);
            _platform.Transport.Deliver(endpoint, partner, sent.Body, sent.FileName);
        }

        private static string PipelineFor(string endpoint)
        {
            return endpoint.StartsWith("AS2.", StringComparison.OrdinalIgnoreCase) ? "As2EdiSend" : "FileEdiSend";
        }

        private bool ApplyAck(string partnerId, string acknowledgedControlNumber, string status)
        {
            if (!Enum.TryParse(status, out AckStatus parsed))
            {
                throw new ArgumentException("Unknown acknowledgement status " + status + ".", nameof(status));
            }

            var number = (acknowledgedControlNumber ?? string.Empty).TrimStart('0');
            lock (_lab.Interchanges)
            {
                var record = _lab.Interchanges.LastOrDefault(i => i.PartnerId == partnerId && i.Direction == InterchangeDirection.Outbound && i.AckStatus == AckStatus.Pending
                    && ((i.ControlNumber ?? string.Empty).TrimStart('0') == number || (i.GroupControlNumber ?? string.Empty).TrimStart('0') == number));
                if (record == null)
                {
                    return false;
                }

                record.ApplyAck(parsed, _lab.UtcNow);
                return true;
            }
        }

        private Monitoring.OrderActivityRow Confirmed(string partnerId, string orderNumber, string erpOrderNumber)
        {
            if (!_lab.Bam.TryGetValue(partnerId + "|" + orderNumber, out var row))
            {
                _lab.Trace("BAM: no OrderLifecycle activity for {0} {1}", partnerId, orderNumber);
                return null;
            }

            row.ErpOrderNumber = erpOrderNumber;
            row.ConfirmedUtc ??= _lab.UtcNow;
            return row;
        }

        private string RoutingSlipFor(string partnerId, string documentKind)
        {
            var partner = _lab.Directory.Get(partnerId);
            var facts = new RoutingFacts(partnerId, partner.Standard.ToString(), partner.Transport.ToString(), documentKind);
            _lab.Policy("VanArsdel.Routing").Execute(facts);
            if (!facts.Resolved)
            {
                throw new InvalidOperationException("Policy VanArsdel.Routing has no route for " + partnerId + " " + documentKind + ".");
            }

            return facts.Slip;
        }

        private string EndpointFor(string partnerId, string documentKind)
        {
            var slip = RoutingSlip.Parse(RoutingSlipFor(partnerId, documentKind));
            var send = slip.Steps.FirstOrDefault(s => s.Kind == RoutingStepKind.Send)
                ?? throw new InvalidOperationException("The routing slip for " + partnerId + " " + documentKind + " has no SEND step.");
            return send.Target;
        }

        private void Execute(string policy, RuleOutcome outcome, params object[] facts)
        {
            var fired = _lab.Policy(policy).Execute(facts.Concat(new object[] { outcome }).ToArray());
            _lab.Trace("BRE {0}: {1}", policy, fired.Count == 0 ? "no rule fired" : string.Join(", ", fired));
        }

        private static void ApplyOutcome(Order order, RuleOutcome outcome)
        {
            if (!string.IsNullOrEmpty(outcome.ShipVia))
            {
                order.Header.ShipVia = outcome.ShipVia;
            }

            if (!string.IsNullOrEmpty(outcome.OrderType))
            {
                order.Header.OrderType = outcome.OrderType;
            }

            if (outcome.Notes.Count > 0)
            {
                order.Header.Notes = string.Join(" / ", new[] { order.Header.Notes }.Concat(outcome.Notes).Where(n => !string.IsNullOrEmpty(n)));
            }
        }

        private static XmlDocument CanonicalToXml(Order order)
        {
            var doc = new XmlDocument();
            doc.LoadXml(CanonicalSerializer.Serialize(order));
            return doc;
        }

        private static string RejectionNotice(TradingPartner partner, string orderNumber, RuleOutcome outcome)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Order " + orderNumber + " from " + partner.Name + " (" + partner.PartnerId + ") was not passed to the ERP.");
            sb.AppendLine();
            foreach (var r in outcome.Rejections)
            {
                sb.AppendLine(" - " + r);
            }

            if (outcome.ManualReview)
            {
                sb.AppendLine(" - manual review requested");
                foreach (var n in outcome.Notes)
                {
                    sb.AppendLine("   " + n);
                }
            }

            sb.AppendLine();
            sb.AppendLine("Rules: " + string.Join(", ", outcome.FiredRules));
            sb.AppendLine("Resubmit from the operations portal after correcting the cross-reference or the order.");
            return sb.ToString();
        }

        private static string Subject(string prefix, string partnerId, string number)
        {
            return string.Format(Inv, "[B2B] {0} {1} {2}", prefix, partnerId, number);
        }

        private static string Safe(string value)
        {
            return new string((value ?? "none").Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '_').ToArray());
        }

        private void Done(string orchestration, string partnerId, string document, string result)
        {
            var run = new OrchestrationRun { Orchestration = orchestration, PartnerId = partnerId, Document = document, Result = result };
            lock (Runs)
            {
                Runs.Add(run);
            }

            _lab.Trace("{0}", run);
        }

        private class PendingAck
        {
            public string PartnerId { get; set; }
            public string ControlNumber { get; set; }
            public string DocumentNumber { get; set; }
            public DateTime DueLocal { get; set; }
            public TimeSpan Timeout { get; set; }
        }
    }
}
