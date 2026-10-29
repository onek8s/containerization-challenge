using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Litware.OrderHub.Core.Warehouse
{
    /// <summary>
    /// Shipment confirmation from the 3PL warehouse: comma separated, header row,
    /// one row per shipped order line. ShipDate is ISO 8601 with offset.
    /// </summary>
    public static class ShipmentConfirmationFile
    {
        public static IReadOnlyList<ShipmentConfirmation> Read(TextReader reader)
        {
            string header = reader.ReadLine();
            if (header == null) return new ShipmentConfirmation[0];

            var columns = CsvLine.Split(header, ',').Select(c => c.Trim()).ToList();
            int iOrder = Require(columns, "OrderNo");
            int iSku = Require(columns, "Sku");
            int iQty = Require(columns, "QtyShipped");
            int iCarrier = Require(columns, "Carrier");
            int iTracking = Require(columns, "TrackingNo");
            int iDate = Require(columns, "ShipDate");

            var result = new List<ShipmentConfirmation>();
            string line;
            int lineNumber = 1;
            while ((line = reader.ReadLine()) != null)
            {
                lineNumber++;
                if (line.Trim().Length == 0) continue;
                var f = CsvLine.Split(line, ',');
                if (f.Count < columns.Count)
                {
                    throw new FormatException("Line " + lineNumber + " has " + f.Count + " fields, expected " + columns.Count + ".");
                }

                string orderNo = f[iOrder].Trim();
                string tracking = f[iTracking].Trim();
                var shipment = result.FirstOrDefault(s => s.OrderNumber == orderNo && s.TrackingNumber == tracking);
                if (shipment == null)
                {
                    shipment = new ShipmentConfirmation
                    {
                        OrderNumber = orderNo,
                        Carrier = f[iCarrier].Trim(),
                        TrackingNumber = tracking,
                        ShippedUtc = DateTimeOffset.Parse(f[iDate].Trim(), CultureInfo.InvariantCulture).UtcDateTime
                    };
                    result.Add(shipment);
                }

                shipment.Lines.Add(new Contracts.ShippedLine
                {
                    Sku = f[iSku].Trim(),
                    Quantity = int.Parse(f[iQty].Trim(), CultureInfo.InvariantCulture)
                });
            }
            return result;
        }

        private static int Require(List<string> columns, string name)
        {
            int index = columns.IndexOf(name);
            if (index < 0) throw new FormatException("Shipment confirmation is missing column " + name + ".");
            return index;
        }
    }

    public class ShipmentConfirmation
    {
        public string OrderNumber { get; set; }
        public string Carrier { get; set; }
        public string TrackingNumber { get; set; }
        public DateTime ShippedUtc { get; set; }
        public List<Contracts.ShippedLine> Lines { get; } = new List<Contracts.ShippedLine>();
    }
}
