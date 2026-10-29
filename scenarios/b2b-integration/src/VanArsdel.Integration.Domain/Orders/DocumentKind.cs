namespace VanArsdel.Integration.Domain.Orders
{
    /// <summary>Business document kinds. The value is promoted as VanArsdel.MessageKind on every message.</summary>
    public enum DocumentKind
    {
        Unknown = 0,
        CustomerOrder = 1,
        OrderResponse = 2,
        Despatch = 3,
        CustomerInvoice = 4,
        PurchaseOrder = 5,
        SupplierInvoice = 6,
        FunctionalAck = 7,
        StockLevel = 8
    }

    public static class DocumentKinds
    {
        /// <summary>Transaction set or message type for a document kind in a standard.</summary>
        public static string ToTransactionType(DocumentKind kind, Partners.DocumentStandard standard)
        {
            var x12 = standard == Partners.DocumentStandard.X12;
            switch (kind)
            {
                case DocumentKind.CustomerOrder:
                case DocumentKind.PurchaseOrder:
                    return x12 ? "850" : "ORDERS";
                case DocumentKind.OrderResponse:
                    return x12 ? "855" : "ORDRSP";
                case DocumentKind.Despatch:
                    return x12 ? "856" : "DESADV";
                case DocumentKind.CustomerInvoice:
                case DocumentKind.SupplierInvoice:
                    return x12 ? "810" : "INVOIC";
                case DocumentKind.FunctionalAck:
                    return x12 ? "997" : "CONTRL";
                default:
                    return null;
            }
        }

        public static DocumentKind FromTransactionType(string type, Partners.PartnerRole senderRole)
        {
            switch ((type ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "850":
                case "ORDERS":
                    return senderRole == Partners.PartnerRole.Customer ? DocumentKind.CustomerOrder : DocumentKind.PurchaseOrder;
                case "855":
                case "ORDRSP":
                    return DocumentKind.OrderResponse;
                case "856":
                case "DESADV":
                    return DocumentKind.Despatch;
                case "810":
                case "INVOIC":
                    return senderRole == Partners.PartnerRole.Supplier ? DocumentKind.SupplierInvoice : DocumentKind.CustomerInvoice;
                case "997":
                case "CONTRL":
                    return DocumentKind.FunctionalAck;
                default:
                    return DocumentKind.Unknown;
            }
        }
    }
}
