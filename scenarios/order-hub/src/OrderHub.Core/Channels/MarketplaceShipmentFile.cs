using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Litware.OrderHub.Core.Channels
{
    /// <summary>Shipping confirmation upload of the marketplace seller centre (one row per order line).</summary>
    public static class MarketplaceShipmentFile
    {
        public const string Header = "OrderRef;Sku;Qty;Carrier;TrackingNo;ShippedAt";

        public static string FileName(string orderNumber)
        {
            return "shipped_" + orderNumber + ".csv";
        }

        public static byte[] Build(string orderRef, string carrier, string trackingNumber, System.DateTime shippedUtc, IEnumerable<Contracts.ShippedLine> lines)
        {
            var sb = new StringBuilder();
            sb.Append(Header).Append("\r\n");
            foreach (var line in lines)
            {
                sb.Append(CsvLine.Quote(orderRef, ';')).Append(';')
                  .Append(CsvLine.Quote(line.Sku, ';')).Append(';')
                  .Append(line.Quantity.ToString(CultureInfo.InvariantCulture)).Append(';')
                  .Append(CsvLine.Quote(carrier, ';')).Append(';')
                  .Append(CsvLine.Quote(trackingNumber, ';')).Append(';')
                  .Append(shippedUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture))
                  .Append("\r\n");
            }
            // The seller centre needs the BOM to detect UTF-8.
            return Concat(new UTF8Encoding(true).GetPreamble(), Encoding.UTF8.GetBytes(sb.ToString()));
        }

        private static byte[] Concat(byte[] a, byte[] b)
        {
            var result = new byte[a.Length + b.Length];
            System.Buffer.BlockCopy(a, 0, result, 0, a.Length);
            System.Buffer.BlockCopy(b, 0, result, a.Length, b.Length);
            return result;
        }
    }
}
