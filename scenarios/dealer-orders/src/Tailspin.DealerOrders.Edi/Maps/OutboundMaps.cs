using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Tailspin.DealerOrders.Edi.X12;

namespace Tailspin.DealerOrders.Edi.Maps
{
    public enum LineAckCode
    {
        /// <summary>IA: item accepted.</summary>
        Accepted,
        /// <summary>IP: item accepted, price changed.</summary>
        PriceChanged,
        /// <summary>DR: item accepted, date rescheduled.</summary>
        DateRescheduled,
        /// <summary>IQ: item accepted, quantity changed.</summary>
        QuantityChanged,
        /// <summary>IR: item rejected.</summary>
        Rejected
    }

    public class PoAcknowledgment
    {
        public string PoNumber { get; set; }
        public DateTime PoDate { get; set; }
        public string OrderNumber { get; set; }
        public bool Rejected { get; set; }
        public string RejectReason { get; set; }
        public string Currency { get; set; }
        public DateTime AckDate { get; set; }
        public List<PoAckLine> Lines { get; } = new List<PoAckLine>();
    }

    public class PoAckLine
    {
        public string LineId { get; set; }
        public decimal OrderedQuantity { get; set; }
        public decimal AcceptedQuantity { get; set; }
        public decimal UnitPrice { get; set; }
        public string VendorPartNumber { get; set; }
        public string BuyerPartNumber { get; set; }
        public DateTime? ScheduledShipDate { get; set; }
        public LineAckCode Code { get; set; }
    }

    /// <summary>Builds the 855 purchase order acknowledgment.</summary>
    public static class PoAcknowledgment855
    {
        public static List<X12Segment> Build(PoAcknowledgment ack)
        {
            var anyChange = ack.Lines.Any(l => l.Code != LineAckCode.Accepted);
            var purpose = ack.Rejected ? "RJ" : anyChange ? "AC" : "AD";
            var segments = new List<X12Segment>
            {
                new X12Segment("BAK", "00", purpose, ack.PoNumber, ack.PoDate, "", "", "", ack.OrderNumber ?? "", ack.AckDate)
            };
            if (!string.IsNullOrEmpty(ack.Currency)) segments.Add(new X12Segment("CUR", "SE", ack.Currency));
            if (ack.Rejected && !string.IsNullOrEmpty(ack.RejectReason)) segments.Add(new X12Segment("MSG", Truncate(ack.RejectReason, 264)));

            foreach (var line in ack.Lines)
            {
                var po1 = new List<object> { line.LineId, line.OrderedQuantity, "EA", line.UnitPrice, "PE" };
                if (!string.IsNullOrEmpty(line.BuyerPartNumber)) { po1.Add("BP"); po1.Add(line.BuyerPartNumber); }
                if (!string.IsNullOrEmpty(line.VendorPartNumber)) { po1.Add("VP"); po1.Add(line.VendorPartNumber); }
                segments.Add(new X12Segment("PO1", po1.ToArray()));
                segments.Add(new X12Segment("ACK", Code(line.Code), line.AcceptedQuantity, "EA",
                    line.ScheduledShipDate.HasValue ? "068" : "", line.ScheduledShipDate.HasValue ? (object)line.ScheduledShipDate.Value : ""));
            }

            segments.Add(new X12Segment("CTT", ack.Lines.Count));
            return segments;
        }

        public static string Code(LineAckCode code)
        {
            switch (code)
            {
                case LineAckCode.Accepted: return "IA";
                case LineAckCode.PriceChanged: return "IP";
                case LineAckCode.DateRescheduled: return "DR";
                case LineAckCode.QuantityChanged: return "IQ";
                default: return "IR";
            }
        }

        private static string Truncate(string text, int max) => text.Length <= max ? text : text.Substring(0, max);
    }

    public class ShipNotice
    {
        public string ShipmentId { get; set; }
        public DateTime ShipDate { get; set; }
        public string CarrierScac { get; set; }
        public string BillOfLading { get; set; }
        public string TrackingNumber { get; set; }
        public string TrailerNumber { get; set; }
        public int Packages { get; set; }
        public decimal GrossWeightKg { get; set; }
        public string ShipFromName { get; set; }
        public string ShipToName { get; set; }
        public string ShipToCode { get; set; }
        public string PoNumber { get; set; }
        public DateTime PoDate { get; set; }
        public string OrderNumber { get; set; }
        public List<ShipNoticeLine> Lines { get; } = new List<ShipNoticeLine>();
    }

    public class ShipNoticeLine
    {
        public string LineId { get; set; }
        public string VendorPartNumber { get; set; }
        public string BuyerPartNumber { get; set; }
        public decimal Quantity { get; set; }
        public decimal OrderedQuantity { get; set; }
        public string SerialNumber { get; set; }
    }

    /// <summary>Builds the 856 ship notice in the Shipment / Order / Item hierarchy (one order per notice).</summary>
    public static class ShipNotice856
    {
        public static List<X12Segment> Build(ShipNotice notice)
        {
            var segments = new List<X12Segment>
            {
                new X12Segment("BSN", "00", notice.ShipmentId, notice.ShipDate, notice.ShipDate.ToString("HHmm", CultureInfo.InvariantCulture), "0001")
            };
            var hl = 1;
            segments.Add(new X12Segment("HL", hl, "", "S"));
            segments.Add(new X12Segment("TD1", "PLT", notice.Packages, "", "", "", "G", Math.Round(notice.GrossWeightKg, 1), "KG"));
            if (!string.IsNullOrEmpty(notice.CarrierScac)) segments.Add(new X12Segment("TD5", "", "2", notice.CarrierScac));
            if (!string.IsNullOrEmpty(notice.TrailerNumber)) segments.Add(new X12Segment("TD3", "TL", "", notice.TrailerNumber));
            if (!string.IsNullOrEmpty(notice.BillOfLading)) segments.Add(new X12Segment("REF", "BM", notice.BillOfLading));
            if (!string.IsNullOrEmpty(notice.TrackingNumber)) segments.Add(new X12Segment("REF", "CN", notice.TrackingNumber));
            segments.Add(new X12Segment("DTM", "011", notice.ShipDate));
            segments.Add(new X12Segment("N1", "SF", notice.ShipFromName));
            segments.Add(string.IsNullOrEmpty(notice.ShipToCode)
                ? new X12Segment("N1", "ST", notice.ShipToName)
                : new X12Segment("N1", "ST", notice.ShipToName, "92", notice.ShipToCode));

            var orderHl = ++hl;
            segments.Add(new X12Segment("HL", orderHl, 1, "O"));
            segments.Add(new X12Segment("PRF", notice.PoNumber, "", "", notice.PoDate));
            if (!string.IsNullOrEmpty(notice.OrderNumber)) segments.Add(new X12Segment("REF", "VN", notice.OrderNumber));

            foreach (var line in notice.Lines)
            {
                segments.Add(new X12Segment("HL", ++hl, orderHl, "I"));
                var lin = new List<object> { line.LineId };
                if (!string.IsNullOrEmpty(line.BuyerPartNumber)) { lin.Add("BP"); lin.Add(line.BuyerPartNumber); }
                lin.Add("VP");
                lin.Add(line.VendorPartNumber);
                if (!string.IsNullOrEmpty(line.SerialNumber)) { lin.Add("SN"); lin.Add(line.SerialNumber); }
                segments.Add(new X12Segment("LIN", lin.ToArray()));
                segments.Add(new X12Segment("SN1", line.LineId, line.Quantity, "EA", "", line.OrderedQuantity, "EA"));
            }

            segments.Add(new X12Segment("CTT", hl));
            return segments;
        }
    }

    /// <summary>Builds a 997 functional acknowledgment for a received functional group.</summary>
    public static class FunctionalAck997
    {
        public static List<X12Segment> Build(X12FunctionalGroup group, IList<X12Error> errors)
        {
            var segments = new List<X12Segment> { new X12Segment("AK1", group.FunctionalId, group.ControlNumber) };
            var accepted = 0;
            foreach (var t in group.Transactions)
            {
                segments.Add(new X12Segment("AK2", t.TransactionSetId, t.ControlNumber));
                var setErrors = errors.Where(e => e.Level == X12ErrorLevel.Transaction && e.ControlNumber == t.ControlNumber).ToList();
                if (setErrors.Count == 0)
                {
                    segments.Add(new X12Segment("AK5", "A"));
                    accepted++;
                }
                else
                {
                    var codes = new List<object> { "R" };
                    codes.AddRange(setErrors.Select(e => (object)e.Code).Distinct().Take(5));
                    segments.Add(new X12Segment("AK5", codes.ToArray()));
                }
            }

            var groupErrors = errors.Where(e => e.Level == X12ErrorLevel.Group && e.ControlNumber == group.ControlNumber).Select(e => (object)e.Code).Distinct().Take(5).ToList();
            var status = groupErrors.Count > 0 || accepted == 0 ? "R" : accepted < group.Transactions.Count ? "P" : "A";
            var ak9 = new List<object> { status, group.Transactions.Count, group.Transactions.Count, accepted };
            ak9.AddRange(groupErrors);
            segments.Add(new X12Segment("AK9", ak9.ToArray()));
            return segments;
        }

        /// <summary>Reads a received 997: returns the acknowledged group control number and whether it was accepted.</summary>
        public static Tuple<string, string, bool> Read(X12Transaction transaction)
        {
            var ak1 = transaction.First("AK1") ?? throw new X12FormatException("AK1 missing in 997.");
            var ak9 = transaction.First("AK9") ?? throw new X12FormatException("AK9 missing in 997.");
            return Tuple.Create(ak1[1], ak1[2], ak9[1] == "A" || ak9[1] == "E");
        }
    }
}
