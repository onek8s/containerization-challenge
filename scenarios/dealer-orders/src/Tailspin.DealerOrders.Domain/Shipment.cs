using System;
using System.Collections.Generic;

namespace Tailspin.DealerOrders.Domain
{
    /// <summary>A packing slip posted in AX (CustPackingSlipJour), used for order status and the 856 ship notice.</summary>
    public class Shipment
    {
        public Shipment()
        {
            Lines = new List<ShipmentLine>();
        }

        public int Id { get; set; }
        public int OrderId { get; set; }
        public string PackingSlipId { get; set; }
        public DateTime ShipDate { get; set; }
        public string CarrierCode { get; set; }
        public string TrackingNumber { get; set; }
        public string TrailerNumber { get; set; }
        public decimal GrossWeightKg { get; set; }
        public int Packages { get; set; }
        public bool AsnSent { get; set; }
        public List<ShipmentLine> Lines { get; set; }
    }

    public class ShipmentLine
    {
        public int Id { get; set; }
        public int ShipmentId { get; set; }
        public int OrderLineNumber { get; set; }
        public string ItemId { get; set; }
        public decimal Quantity { get; set; }
        public string SerialNumber { get; set; }
    }
}
