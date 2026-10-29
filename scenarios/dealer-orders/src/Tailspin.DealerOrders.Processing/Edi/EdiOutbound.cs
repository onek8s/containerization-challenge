using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using log4net;
using Tailspin.DealerOrders.Domain;
using Tailspin.DealerOrders.Edi.Maps;
using Tailspin.DealerOrders.Edi.X12;
using Tailspin.DealerOrders.Processing.Ports;

namespace Tailspin.DealerOrders.Processing.Edi
{
    /// <summary>Creates outbound X12 interchanges (855, 856, 997) and queues them for the EDI gateway.</summary>
    public class EdiOutbound
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(EdiOutbound));

        private readonly IEdiStore _store;
        private readonly IClock _clock;
        private readonly ProcessingSettings _settings;

        public EdiOutbound(IEdiStore store, IClock clock, ProcessingSettings settings)
        {
            _store = store;
            _clock = clock;
            _settings = settings;
        }

        public void QueueAcknowledgment(SalesOrder order, Dealer dealer)
        {
            var partner = _store.PartnerForDealer(dealer.Id);
            if (partner == null || !partner.Active || !partner.Send855) return;
            var original = OriginalPo(order);
            var ack = new PoAcknowledgment
            {
                PoNumber = order.DealerPoNumber,
                PoDate = original?.PoDate ?? order.CreatedUtc.Date,
                OrderNumber = order.AxSalesId ?? order.OrderNumber,
                Currency = order.Currency,
                AckDate = _clock.Today,
                Rejected = order.Status == OrderStatus.Rejected || order.Status == OrderStatus.Cancelled,
                RejectReason = order.ErrorMessage
            };
            foreach (var line in order.Lines.OrderBy(l => l.LineNumber))
            {
                var poLine = original?.Lines.FirstOrDefault(l => l.LineId == line.DealerLineReference);
                var code = LineAckCode.Accepted;
                if (ack.Rejected || line.Cancelled) code = LineAckCode.Rejected;
                else if (poLine?.UnitPrice != null && Math.Abs(poLine.UnitPrice.Value - line.NetPrice) > line.NetPrice * _settings.PriceTolerancePercent / 100m) code = LineAckCode.PriceChanged;
                else if (order.RequestedDeliveryDate.HasValue && line.ConfirmedShipDate.HasValue && line.ConfirmedShipDate.Value.Date > order.RequestedDeliveryDate.Value.Date) code = LineAckCode.DateRescheduled;
                ack.Lines.Add(new PoAckLine
                {
                    LineId = line.DealerLineReference ?? line.LineNumber.ToString(CultureInfo.InvariantCulture),
                    OrderedQuantity = poLine?.Quantity ?? line.Quantity,
                    AcceptedQuantity = code == LineAckCode.Rejected ? 0 : line.Quantity,
                    UnitPrice = line.NetPrice,
                    VendorPartNumber = poLine?.VendorPartNumber ?? line.ItemId,
                    BuyerPartNumber = poLine?.BuyerPartNumber ?? line.DealerItemNumber,
                    ScheduledShipDate = line.ConfirmedShipDate ?? order.ConfirmedDeliveryDate,
                    Code = code
                });
            }
            Queue(partner, "855", "PR", PoAcknowledgment855.Build(ack), order.DealerPoNumber, order.Id);
        }

        /// <summary>855 rejecting a purchase order that could not be turned into an order at all.</summary>
        public void QueueRejection(EdiPartner partner, DealerPurchaseOrder po, string reason)
        {
            if (!partner.Send855) return;
            var ack = new PoAcknowledgment { PoNumber = po.PoNumber, PoDate = po.PoDate, AckDate = _clock.Today, Rejected = true, RejectReason = reason, Currency = po.Currency };
            foreach (var line in po.Lines)
                ack.Lines.Add(new PoAckLine
                {
                    LineId = line.LineId,
                    OrderedQuantity = line.Quantity,
                    AcceptedQuantity = 0,
                    UnitPrice = line.UnitPrice ?? 0,
                    VendorPartNumber = line.VendorPartNumber,
                    BuyerPartNumber = line.BuyerPartNumber,
                    Code = LineAckCode.Rejected
                });
            Queue(partner, "855", "PR", PoAcknowledgment855.Build(ack), po.PoNumber, null);
        }

        public void QueueShipNotice(SalesOrder order, Dealer dealer, Shipment shipment)
        {
            var partner = _store.PartnerForDealer(dealer.Id);
            if (partner == null || !partner.Active || !partner.Send856 || shipment.AsnSent) return;
            var original = OriginalPo(order);
            var notice = new ShipNotice
            {
                ShipmentId = shipment.PackingSlipId,
                ShipDate = shipment.ShipDate,
                CarrierScac = shipment.CarrierCode,
                BillOfLading = shipment.PackingSlipId,
                TrackingNumber = shipment.TrackingNumber,
                TrailerNumber = shipment.TrailerNumber,
                Packages = Math.Max(1, shipment.Packages),
                GrossWeightKg = shipment.GrossWeightKg,
                ShipFromName = _settings.ShipFromName,
                ShipToName = order.DeliveryAddress?.Name ?? dealer.Name,
                ShipToCode = order.DeliveryAddress?.EdiLocationCode,
                PoNumber = order.DealerPoNumber,
                PoDate = original?.PoDate ?? order.CreatedUtc.Date,
                OrderNumber = order.AxSalesId
            };
            foreach (var line in shipment.Lines)
            {
                var orderLine = order.Lines.FirstOrDefault(l => l.LineNumber == line.OrderLineNumber);
                var poLine = original?.Lines.FirstOrDefault(l => l.LineId == orderLine?.DealerLineReference);
                notice.Lines.Add(new ShipNoticeLine
                {
                    LineId = orderLine?.DealerLineReference ?? line.OrderLineNumber.ToString(CultureInfo.InvariantCulture),
                    VendorPartNumber = poLine?.VendorPartNumber ?? line.ItemId,
                    BuyerPartNumber = poLine?.BuyerPartNumber ?? orderLine?.DealerItemNumber,
                    Quantity = line.Quantity,
                    OrderedQuantity = orderLine?.Quantity ?? line.Quantity,
                    SerialNumber = line.SerialNumber
                });
            }
            Queue(partner, "856", "SH", ShipNotice856.Build(notice), shipment.PackingSlipId, order.Id);
            shipment.AsnSent = true;
        }

        public void QueueFunctionalAck(EdiPartner partner, X12FunctionalGroup group, IList<X12Error> errors) =>
            Queue(partner, "997", "FA", FunctionalAck997.Build(group, errors), group.FunctionalId + " " + group.ControlNumber, null);

        private void Queue(EdiPartner partner, string transactionSet, string functionalId, List<X12Segment> body, string reference, int? orderId)
        {
            var control = _store.NextControlNumber(partner.Id);
            var writer = new X12Writer();
            writer.AddTransaction(transactionSet, body);
            var text = writer.Write(
                new X12Party(_settings.OurInterchangeQualifier, _settings.OurInterchangeId, _settings.OurApplicationCode),
                new X12Party(partner.InterchangeQualifier, partner.InterchangeId, partner.ApplicationCode),
                control, control, functionalId, _clock.UtcNow.ToLocalTime(), _settings.EdiTestIndicator, requestAck: transactionSet != "997");
            _store.AddMessage(new EdiMessage
            {
                PartnerId = partner.Id,
                Direction = EdiDirection.Outbound,
                TransactionSet = transactionSet,
                InterchangeControl = control.ToString("000000000", CultureInfo.InvariantCulture),
                GroupControl = control.ToString(CultureInfo.InvariantCulture),
                TransactionControl = "0001",
                Reference = reference,
                State = EdiMessageState.Queued,
                Content = text,
                CreatedUtc = _clock.UtcNow,
                OrderId = orderId
            });
            Log.InfoFormat("Queued {0} {1} for {2} (ISA {3})", transactionSet, reference, partner.Name, control);
        }

        private DealerPurchaseOrder OriginalPo(SalesOrder order)
        {
            if (!order.EdiMessageId.HasValue) return null;
            var message = _store.GetMessage(order.EdiMessageId.Value);
            if (message == null) return null;
            try
            {
                var interchange = X12Reader.Read(message.Content, out _);
                return interchange.Groups.SelectMany(g => g.Transactions).Where(t => t.TransactionSetId == "850")
                    .Select(PurchaseOrder850.Map).FirstOrDefault(p => p.PoNumber == order.DealerPoNumber);
            }
            catch (X12FormatException ex)
            {
                Log.Warn($"Original 850 of order {order.OrderNumber} could not be read again", ex);
                return null;
            }
        }
    }
}
