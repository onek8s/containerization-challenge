using System;
using System.Xml.Serialization;

namespace VanArsdel.Integration.Contracts.Gateway
{
    /// <summary>
    /// Order document of the partner gateway (schema PartnerOrders.xsd, 2016). Small trade customers without EDI
    /// send these from their own systems or from the web order form of the contractor portal.
    /// </summary>
    [Serializable]
    [XmlType(Namespace = MessageTypes.PartnerOrdersNamespace)]
    [XmlRoot("OrderBatch", Namespace = MessageTypes.PartnerOrdersNamespace, IsNullable = false)]
    public class OrderBatch
    {
        [XmlAttribute]
        public string BatchId { get; set; }

        [XmlAttribute]
        public string PartnerId { get; set; }

        [XmlAttribute]
        public DateTime Created { get; set; }

        [XmlElement("Order")]
        public PartnerOrder[] Orders { get; set; }
    }

    [Serializable]
    [XmlType("Order", Namespace = MessageTypes.PartnerOrdersNamespace)]
    public class PartnerOrder
    {
        public string PoNumber { get; set; }

        [XmlElement(DataType = "date")]
        public DateTime OrderDate { get; set; }

        [XmlElement(DataType = "date")]
        public DateTime DeliverBy { get; set; }

        [XmlIgnore]
        public bool DeliverBySpecified { get; set; }

        public string AccountNumber { get; set; }

        public DeliveryAddress DeliveryAddress { get; set; }

        public string Contact { get; set; }

        public string Phone { get; set; }

        public string Instructions { get; set; }

        [XmlElement("Item")]
        public PartnerOrderItem[] Items { get; set; }
    }

    [Serializable]
    [XmlType(Namespace = MessageTypes.PartnerOrdersNamespace)]
    public class DeliveryAddress
    {
        public string Name { get; set; }
        public string Line1 { get; set; }
        public string Line2 { get; set; }
        public string Town { get; set; }
        public string Postcode { get; set; }
        public string Country { get; set; }
    }

    [Serializable]
    [XmlType("Item", Namespace = MessageTypes.PartnerOrdersNamespace)]
    public class PartnerOrderItem
    {
        public string Sku { get; set; }
        public decimal Qty { get; set; }
        public string Unit { get; set; }
        public decimal Price { get; set; }

        [XmlIgnore]
        public bool PriceSpecified { get; set; }

        public string Text { get; set; }
    }
}
