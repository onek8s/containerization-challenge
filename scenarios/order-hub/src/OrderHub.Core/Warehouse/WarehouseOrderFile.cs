using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Litware.OrderHub.Core.Warehouse
{
    /// <summary>
    /// Outbound order for the 3PL warehouse (A. Datum Logistics WMS order interface v1).
    /// One file per order, uploaded to the "in" folder of the Litware account.
    /// </summary>
    public static class WarehouseOrderFile
    {
        public static readonly XNamespace Ns = "urn:adatum:wms:order:1";

        public static string FileName(OrderRecord order)
        {
            return "ORD_" + order.OrderNumber + ".xml";
        }

        public static XDocument Build(OrderRecord order, string clientCode)
        {
            var doc = order.Document;
            var ship = doc.ShipTo;
            int lineNo = 0;

            return new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement(Ns + "OrderRequest",
                    new XAttribute("client", clientCode),
                    new XElement(Ns + "OrderNo", order.OrderNumber),
                    new XElement(Ns + "Reference", Truncate(doc.CustomerReference, 35)),
                    new XElement(Ns + "Channel", order.Channel),
                    new XElement(Ns + "Service", ServiceCode(doc.ShippingMethod)),
                    new XElement(Ns + "Consignee",
                        new XElement(Ns + "Name", Truncate(ship.Name, 60)),
                        new XElement(Ns + "Address1", Truncate(ship.Street, 60)),
                        string.IsNullOrEmpty(ship.Street2) ? null : new XElement(Ns + "Address2", Truncate(ship.Street2, 60)),
                        new XElement(Ns + "Zip", ship.PostalCode),
                        new XElement(Ns + "City", ship.City),
                        new XElement(Ns + "Country", ship.CountryCode),
                        string.IsNullOrEmpty(doc.Phone) ? null : new XElement(Ns + "Phone", doc.Phone),
                        string.IsNullOrEmpty(doc.CustomerEmail) ? null : new XElement(Ns + "Email", doc.CustomerEmail)),
                    new XElement(Ns + "Lines",
                        doc.Lines.Select(l => new XElement(Ns + "Line",
                            new XAttribute("no", ++lineNo),
                            new XElement(Ns + "Sku", l.Sku),
                            new XElement(Ns + "Qty", l.Quantity))))));
        }

        public static byte[] ToBytes(XDocument document)
        {
            var settings = new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true };
            using (var stream = new MemoryStream())
            {
                using (var writer = XmlWriter.Create(stream, settings))
                {
                    document.Save(writer);
                }
                return stream.ToArray();
            }
        }

        private static string ServiceCode(string shippingMethod)
        {
            switch (shippingMethod)
            {
                case "express":
                case "flat_rate_express":
                    return "EXP";
                case "local_pickup":
                    return "PICKUP";
                case "dropship":
                    return "DS";
                default:
                    return "STD";
            }
        }

        private static string Truncate(string value, int length)
        {
            if (value == null) return null;
            return value.Length <= length ? value : value.Substring(0, length);
        }
    }
}
