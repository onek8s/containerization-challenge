using System;

namespace Contoso.Purchasing.Core
{
    public class PurchaseOrder
    {
        public int Id { get; set; }
        public string PoNumber { get; set; }
        public int RequestId { get; set; }
        public string RequestNumber { get; set; }
        public string BuyerAccount { get; set; }
        public DateTime OrderDate { get; set; }
        public string DeliveryAddress { get; set; }
        public DateTime? ExportedAt { get; set; }
        public string ExportFile { get; set; }

        public PurchaseRequest Request { get; set; }
        public Supplier Supplier { get; set; }
    }
}
