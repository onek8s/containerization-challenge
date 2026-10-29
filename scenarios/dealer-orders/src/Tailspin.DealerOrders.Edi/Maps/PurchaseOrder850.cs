using System;
using System.Collections.Generic;
using System.Linq;
using Tailspin.DealerOrders.Edi.X12;

namespace Tailspin.DealerOrders.Edi.Maps
{
    public enum PoPurpose
    {
        Original,
        Cancellation,
        Replace,
        Duplicate
    }

    public class DealerPurchaseOrder
    {
        public string TransactionControl { get; set; }
        public PoPurpose Purpose { get; set; }
        public string PoType { get; set; }
        public string PoNumber { get; set; }
        public DateTime PoDate { get; set; }
        public string Currency { get; set; }
        public DateTime? RequestedDelivery { get; set; }
        public string ShipToName { get; set; }
        public string ShipToCode { get; set; }
        public string ShipToStreet { get; set; }
        public string ShipToCity { get; set; }
        public string ShipToState { get; set; }
        public string ShipToPostalCode { get; set; }
        public string ShipToCountry { get; set; }
        public string BuyerName { get; set; }
        public string EndCustomer { get; set; }
        public List<string> Notes { get; } = new List<string>();
        public List<DealerPoLine> Lines { get; } = new List<DealerPoLine>();
        public int? DeclaredLineCount { get; set; }
    }

    public class DealerPoLine
    {
        public string LineId { get; set; }
        public decimal Quantity { get; set; }
        public string Uom { get; set; }
        public decimal? UnitPrice { get; set; }
        public string VendorPartNumber { get; set; }
        public string BuyerPartNumber { get; set; }
        public string Description { get; set; }

        /// <summary>Configuration options sent as SLN sub-lines with the VP qualifier.</summary>
        public List<string> OptionItemIds { get; } = new List<string>();
    }

    /// <summary>Maps an X12 4010 850 transaction to a dealer purchase order.</summary>
    public static class PurchaseOrder850
    {
        public static DealerPurchaseOrder Map(X12Transaction transaction)
        {
            if (transaction.TransactionSetId != "850")
                throw new X12FormatException($"Expected 850, got {transaction.TransactionSetId}.");
            var po = new DealerPurchaseOrder { TransactionControl = transaction.ControlNumber };

            var beg = transaction.First("BEG") ?? throw new X12FormatException("BEG segment missing.");
            po.Purpose = Purpose(beg[1]);
            po.PoType = beg[2];
            po.PoNumber = beg[3];
            if (string.IsNullOrWhiteSpace(po.PoNumber)) throw new X12FormatException("BEG03 purchase order number is empty.");
            po.PoDate = beg.Date(5) ?? throw new X12FormatException("BEG05 purchase order date is empty.");
            po.Currency = transaction.First("CUR")?[2];

            string currentN1 = null;
            DealerPoLine currentLine = null;
            foreach (var s in transaction.Segments)
            {
                switch (s.Id)
                {
                    case "DTM":
                        if (currentLine == null && (s[1] == "002" || s[1] == "010")) po.RequestedDelivery = s.Date(2);
                        break;
                    case "N1":
                        currentN1 = s[1];
                        if (currentN1 == "ST")
                        {
                            po.ShipToName = s[2];
                            if (s[3] == "92" || s[3] == "91") po.ShipToCode = s[4];
                        }
                        else if (currentN1 == "BY") po.BuyerName = s[2];
                        else if (currentN1 == "EN" || currentN1 == "UC") po.EndCustomer = s[2];
                        break;
                    case "N3":
                        if (currentN1 == "ST") po.ShipToStreet = s[1];
                        break;
                    case "N4":
                        if (currentN1 == "ST")
                        {
                            po.ShipToCity = s[1];
                            po.ShipToState = s[2];
                            po.ShipToPostalCode = s[3];
                            po.ShipToCountry = string.IsNullOrEmpty(s[4]) ? "US" : s[4];
                        }
                        break;
                    case "MSG":
                        po.Notes.Add(s[1]);
                        break;
                    case "PO1":
                        currentLine = new DealerPoLine
                        {
                            LineId = s[1],
                            Quantity = s.Decimal(2),
                            Uom = s[3],
                            UnitPrice = string.IsNullOrEmpty(s[4]) ? (decimal?)null : s.Decimal(4),
                            VendorPartNumber = s.ValueAfterQualifier("VP", 6),
                            BuyerPartNumber = s.ValueAfterQualifier("BP", 6)
                        };
                        if (currentLine.Quantity <= 0) throw new X12FormatException($"PO1 {currentLine.LineId}: quantity must be positive.");
                        po.Lines.Add(currentLine);
                        break;
                    case "PID":
                        if (currentLine != null && s[1] == "F") currentLine.Description = s[5];
                        break;
                    case "SLN":
                        if (currentLine == null) throw new X12FormatException("SLN before the first PO1.");
                        var option = s.ValueAfterQualifier("VP", 9);
                        if (!string.IsNullOrEmpty(option)) currentLine.OptionItemIds.Add(option);
                        break;
                    case "CTT":
                        po.DeclaredLineCount = (int)s.Decimal(1);
                        break;
                }
            }

            if (po.Lines.Count == 0) throw new X12FormatException("Purchase order has no PO1 lines.");
            if (po.DeclaredLineCount.HasValue && po.DeclaredLineCount.Value != po.Lines.Count)
                throw new X12FormatException($"CTT01 says {po.DeclaredLineCount} lines, found {po.Lines.Count}.");
            var duplicate = po.Lines.GroupBy(l => l.LineId).FirstOrDefault(g => g.Count() > 1);
            if (duplicate != null) throw new X12FormatException($"Line id {duplicate.Key} is used more than once.");
            return po;
        }

        private static PoPurpose Purpose(string code)
        {
            switch (code)
            {
                case "00": return PoPurpose.Original;
                case "01": return PoPurpose.Cancellation;
                case "05": return PoPurpose.Replace;
                case "07": return PoPurpose.Duplicate;
                default: throw new X12FormatException($"BEG01 purpose {code} is not supported.");
            }
        }
    }
}
