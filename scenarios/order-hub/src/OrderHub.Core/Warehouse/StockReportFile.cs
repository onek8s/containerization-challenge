using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Litware.OrderHub.Core.Warehouse
{
    /// <summary>
    /// Stock snapshot from the 3PL warehouse (STOCK_yyyyMMddHHmm.csv, every 30 minutes):
    /// Sku,OnHand,Allocated,Blocked. Available = OnHand - Allocated - Blocked.
    /// </summary>
    public static class StockReportFile
    {
        public static IReadOnlyList<StockLevel> Read(TextReader reader)
        {
            string header = reader.ReadLine();
            if (header == null) return new StockLevel[0];
            var columns = CsvLine.Split(header, ',').Select(c => c.Trim()).ToList();
            int iSku = Index(columns, "Sku"), iOnHand = Index(columns, "OnHand"), iAllocated = Index(columns, "Allocated");
            int iBlocked = columns.IndexOf("Blocked");

            var result = new Dictionary<string, StockLevel>(StringComparer.OrdinalIgnoreCase);
            string line;
            int lineNumber = 1;
            while ((line = reader.ReadLine()) != null)
            {
                lineNumber++;
                if (line.Trim().Length == 0) continue;
                var f = CsvLine.Split(line, ',');
                try
                {
                    string sku = f[iSku].Trim();
                    int onHand = int.Parse(f[iOnHand], CultureInfo.InvariantCulture);
                    int allocated = int.Parse(f[iAllocated], CultureInfo.InvariantCulture);
                    int blocked = iBlocked >= 0 && f.Count > iBlocked && f[iBlocked].Trim().Length > 0 ? int.Parse(f[iBlocked], CultureInfo.InvariantCulture) : 0;

                    // The same SKU can be reported per storage location; levels are summed.
                    StockLevel level;
                    if (!result.TryGetValue(sku, out level))
                    {
                        level = new StockLevel { Sku = sku };
                        result.Add(sku, level);
                    }
                    level.OnHand += onHand;
                    level.Allocated += allocated;
                    level.Blocked += blocked;
                }
                catch (Exception ex) when (ex is FormatException || ex is ArgumentOutOfRangeException || ex is OverflowException)
                {
                    throw new FormatException("Stock report line " + lineNumber + " is not valid.", ex);
                }
            }
            return result.Values.ToList();
        }

        public static DateTime? SnapshotTime(string fileName)
        {
            string name = Path.GetFileNameWithoutExtension(fileName);
            if (!name.StartsWith("STOCK_", StringComparison.OrdinalIgnoreCase)) return null;
            DateTime value;
            return DateTime.TryParseExact(name.Substring(6), "yyyyMMddHHmm", CultureInfo.InvariantCulture, DateTimeStyles.None, out value)
                ? value : (DateTime?)null;
        }

        private static int Index(List<string> columns, string name)
        {
            int index = columns.IndexOf(name);
            if (index < 0) throw new FormatException("Stock report is missing column " + name + ".");
            return index;
        }
    }

    public class StockLevel
    {
        public string Sku { get; set; }
        public int OnHand { get; set; }
        public int Allocated { get; set; }
        public int Blocked { get; set; }
        public int Available => Math.Max(0, OnHand - Allocated - Blocked);
    }
}
