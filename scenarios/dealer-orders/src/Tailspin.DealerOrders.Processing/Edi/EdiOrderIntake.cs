using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using log4net;
using Tailspin.DealerOrders.Contracts.Portal;
using Tailspin.DealerOrders.Domain;
using Tailspin.DealerOrders.Edi.Maps;
using Tailspin.DealerOrders.Edi.X12;
using Tailspin.DealerOrders.Processing.Ports;
using Tailspin.DealerOrders.Processing.Sales;

namespace Tailspin.DealerOrders.Processing.Edi
{
    public class IntakeResult
    {
        public string InterchangeControl { get; set; }
        public int Received { get; set; }
        public List<string> CreatedOrders { get; } = new List<string>();
        public List<string> Rejected { get; } = new List<string>();
        public List<string> Acknowledged { get; } = new List<string>();
        public List<X12Error> EnvelopeErrors { get; } = new List<X12Error>();
    }

    /// <summary>
    /// Turns received X12 interchanges into dealer orders: 850 originals create orders, 850 cancellations cancel them,
    /// 997s acknowledge our outbound messages. Every functional group of purchase orders is answered with a 997.
    /// </summary>
    public class EdiOrderIntake
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(EdiOrderIntake));

        private readonly IEdiStore _store;
        private readonly IDealerStore _dealers;
        private readonly IOrderStore _orders;
        private readonly ICatalog _catalog;
        private readonly OrderService _orderService;
        private readonly EdiOutbound _outbound;
        private readonly IClock _clock;

        public EdiOrderIntake(IEdiStore store, IDealerStore dealers, IOrderStore orders, ICatalog catalog, OrderService orderService, EdiOutbound outbound, IClock clock)
        {
            _store = store;
            _dealers = dealers;
            _orders = orders;
            _catalog = catalog;
            _orderService = orderService;
            _outbound = outbound;
            _clock = clock;
        }

        public IntakeResult Receive(string content, string as2MessageId, EdiPartner expectedPartner = null)
        {
            var interchange = X12Reader.Read(content, out var errors);
            var partner = _store.FindPartner(interchange.SenderQualifier, interchange.SenderId)
                ?? throw new X12FormatException($"Unknown sender {interchange.SenderQualifier}/{interchange.SenderId}.");
            if (expectedPartner != null && expectedPartner.Id != partner.Id)
                throw new X12FormatException($"Interchange sender {partner.Name} does not match the transport partner {expectedPartner.Name}.");
            if (!partner.Active) throw new X12FormatException($"Partner {partner.Name} is not active.");

            var result = new IntakeResult { InterchangeControl = interchange.ControlNumber };
            result.EnvelopeErrors.AddRange(errors);
            var message = new EdiMessage
            {
                PartnerId = partner.Id,
                Direction = EdiDirection.Inbound,
                TransactionSet = string.Join(",", interchange.Groups.SelectMany(g => g.Transactions).Select(t => t.TransactionSetId).Distinct()),
                InterchangeControl = interchange.ControlNumber,
                GroupControl = string.Join(",", interchange.Groups.Select(g => g.ControlNumber)),
                State = EdiMessageState.Received,
                Content = content,
                As2MessageId = as2MessageId,
                CreatedUtc = _clock.UtcNow
            };
            _store.AddMessage(message);

            foreach (var group in interchange.Groups)
            {
                if (group.FunctionalId == "FA")
                {
                    foreach (var t in group.Transactions.Where(x => x.TransactionSetId == "997"))
                    {
                        var ack = FunctionalAck997.Read(t);
                        var outbound = _store.FindOutbound(partner.Id, ack.Item2);
                        if (outbound != null)
                        {
                            outbound.State = ack.Item3 ? EdiMessageState.Acknowledged : EdiMessageState.Failed;
                            if (!ack.Item3) outbound.Error = "Rejected in 997 from partner";
                            _store.SaveMessage(outbound);
                        }
                        result.Acknowledged.Add(ack.Item1 + " " + ack.Item2 + (ack.Item3 ? " accepted" : " rejected"));
                    }
                    continue;
                }

                var groupErrors = new List<X12Error>(errors);
                foreach (var t in group.Transactions)
                {
                    if (t.TransactionSetId != "850")
                    {
                        groupErrors.Add(new X12Error(X12ErrorLevel.Transaction, t.ControlNumber, "1", $"Transaction set {t.TransactionSetId} not supported."));
                        continue;
                    }
                    result.Received++;
                    if (errors.Any(e => e.Level == X12ErrorLevel.Transaction && e.ControlNumber == t.ControlNumber)) continue;

                    DealerPurchaseOrder po;
                    try
                    {
                        po = PurchaseOrder850.Map(t);
                    }
                    catch (X12FormatException ex)
                    {
                        groupErrors.Add(new X12Error(X12ErrorLevel.Transaction, t.ControlNumber, "5", ex.Message));
                        result.Rejected.Add(t.ControlNumber + ": " + ex.Message);
                        continue;
                    }

                    Handle(partner, po, message, result);
                }

                if (group.FunctionalId == "PO") _outbound.QueueFunctionalAck(partner, group, groupErrors);
            }

            message.State = result.Rejected.Count > 0 && result.CreatedOrders.Count == 0 && result.Received > 0 ? EdiMessageState.Rejected : EdiMessageState.Processed;
            message.Reference = string.Join(",", result.CreatedOrders.Concat(result.Rejected.Select(r => r.Split(':')[0])).Take(5));
            if (result.Rejected.Count > 0) message.Error = string.Join("; ", result.Rejected);
            _store.SaveMessage(message);
            return result;
        }

        private void Handle(EdiPartner partner, DealerPurchaseOrder po, EdiMessage message, IntakeResult result)
        {
            var dealer = _dealers.Get(partner.DealerId);
            try
            {
                switch (po.Purpose)
                {
                    case PoPurpose.Cancellation:
                        var existing = _orders.FindByDealerPo(dealer.Id, po.PoNumber) ?? throw new SalesRuleException($"PO {po.PoNumber} is not known.");
                        _orderService.Cancel(existing.Id, "edi:" + partner.Name, "Cancelled by 850 from dealer");
                        result.CreatedOrders.Add(existing.OrderNumber + " (cancel)");
                        return;
                    case PoPurpose.Duplicate:
                        Log.InfoFormat("Duplicate 850 {0} from {1} ignored", po.PoNumber, partner.Name);
                        return;
                    case PoPurpose.Replace:
                        throw new SalesRuleException("Replacement purchase orders are not accepted; cancel and send a new purchase order.");
                }

                if (!string.IsNullOrEmpty(po.Currency) && !string.Equals(po.Currency, dealer.Currency, StringComparison.OrdinalIgnoreCase))
                    throw new SalesRuleException($"Currency {po.Currency} does not match the dealer currency {dealer.Currency}.");

                var request = new NewOrderRequest
                {
                    DealerId = dealer.Id,
                    User = "edi:" + partner.Name,
                    Source = OrderSource.Edi,
                    DealerPoNumber = po.PoNumber,
                    RequestedDeliveryDate = po.RequestedDelivery.HasValue && po.RequestedDelivery.Value.Date >= _clock.Today ? po.RequestedDelivery : null,
                    DeliveryAddressId = ShipTo(dealer, po)?.Id,
                    EndCustomerName = po.EndCustomer,
                    Notes = po.Notes.Count == 0 ? null : string.Join(Environment.NewLine, po.Notes),
                    EdiMessageId = message.Id
                };

                foreach (var line in po.Lines)
                {
                    var model = _catalog.FindByItemId(line.VendorPartNumber);
                    if (model != null)
                    {
                        var options = new List<string>();
                        foreach (var item in line.OptionItemIds)
                        {
                            var option = model.AllOptions.FirstOrDefault(o => string.Equals(o.ItemId, item, StringComparison.OrdinalIgnoreCase))
                                ?? throw new SalesRuleException($"Line {line.LineId}: option {item} does not exist for {model.ModelCode}.");
                            options.Add(option.Code);
                        }
                        request.Machines.Add(new ConfigurationRequest { ModelCode = model.ModelCode, Options = options, Quantity = line.Quantity });
                        request.MachineLineReferences.Add(line.LineId);
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(line.VendorPartNumber)) throw new SalesRuleException($"Line {line.LineId} has no vendor part number (VP).");
                        request.Parts.Add(new PartLineRequest
                        {
                            ItemId = line.VendorPartNumber,
                            Quantity = line.Quantity,
                            DealerLineReference = line.LineId,
                            DealerItemNumber = line.BuyerPartNumber
                        });
                    }
                }

                // Machine lines are created before part lines; keep the dealer's line ids on both.
                var order = _orderService.CreateOrder(request);
                message.OrderId = order.Id;
                result.CreatedOrders.Add(order.OrderNumber);
                Log.InfoFormat("850 {0} from {1} created order {2} ({3})", po.PoNumber, partner.Name, order.OrderNumber, order.Status);
                if (order.Status == OrderStatus.Rejected) _outbound.QueueAcknowledgment(order, dealer);
            }
            catch (SalesRuleException ex)
            {
                result.Rejected.Add(po.PoNumber + ": " + ex.Message);
                Log.WarnFormat("850 {0} from {1} rejected: {2}", po.PoNumber, partner.Name, ex.Message);
                _outbound.QueueRejection(partner, po, ex.Message);
            }
        }

        private static DealerAddress ShipTo(Dealer dealer, DealerPurchaseOrder po)
        {
            if (!string.IsNullOrEmpty(po.ShipToCode))
            {
                var byCode = dealer.Addresses.FirstOrDefault(a => string.Equals(a.EdiLocationCode, po.ShipToCode, StringComparison.OrdinalIgnoreCase));
                if (byCode != null) return byCode;
                throw new SalesRuleException($"Ship-to location {po.ShipToCode} is not set up for the dealer.");
            }
            return dealer.Addresses.FirstOrDefault(a => a.IsDefaultDelivery);
        }
    }
}
