using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Contoso.Purchasing.Core
{
    /// <summary>
    /// Writes the semicolon separated PO import file read by the ERP inbound interface (format PURCH01).
    /// H;PoNumber;VendorNo;OrderDate;CostCenter;Currency;Buyer;Reference
    /// L;PoNumber;LineNo;Description;Quantity;Unit;UnitPrice;VatRate
    /// T;OrderCount;LineCount;NetTotal
    /// </summary>
    public class ErpOrderExport
    {
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        public static readonly Encoding FileEncoding = Encoding.GetEncoding("ISO-8859-1");

        public string FileName(DateTime timestamp)
        {
            return "PURCH01_" + timestamp.ToString("yyyyMMdd_HHmmss", Invariant) + ".csv";
        }

        public void Write(TextWriter writer, IEnumerable<PurchaseOrder> orders)
        {
            var orderCount = 0;
            var lineCount = 0;
            var netTotal = 0m;

            foreach (var order in orders)
            {
                var request = order.Request;
                orderCount++;

                writer.Write(string.Join(";", new[]
                {
                    "H",
                    order.PoNumber,
                    Clean(order.Supplier.ErpVendorNo),
                    order.OrderDate.ToString("yyyyMMdd", Invariant),
                    Clean(request.CostCenterCode),
                    request.Currency,
                    Clean(ShortAccount(order.BuyerAccount)),
                    Clean(Truncate(request.Number + " " + request.Title, 40))
                }));
                writer.Write("\r\n");

                foreach (var line in request.Lines)
                {
                    lineCount++;
                    netTotal += line.NetAmount;
                    writer.Write(string.Join(";", new[]
                    {
                        "L",
                        order.PoNumber,
                        (line.LineNo * 10).ToString(Invariant),
                        Clean(Truncate(line.Description, 60)),
                        line.Quantity.ToString("0.###", Invariant),
                        Clean(string.IsNullOrEmpty(line.Unit) ? "PCS" : line.Unit.ToUpperInvariant()),
                        line.UnitPrice.ToString("0.00##", Invariant),
                        line.VatRate.ToString("0.0#", Invariant)
                    }));
                    writer.Write("\r\n");
                }
            }

            writer.Write(string.Join(";", new[]
            {
                "T",
                orderCount.ToString(Invariant),
                lineCount.ToString(Invariant),
                netTotal.ToString("0.00", Invariant)
            }));
            writer.Write("\r\n");
        }

        public static string ShortAccount(string account)
        {
            if (string.IsNullOrEmpty(account)) return account;
            var slash = account.LastIndexOf('\\');
            return slash >= 0 ? account.Substring(slash + 1) : account;
        }

        private static string Clean(string value)
        {
            if (value == null) return string.Empty;
            return value.Replace(";", ",").Replace("\r", " ").Replace("\n", " ").Trim();
        }

        private static string Truncate(string value, int length)
        {
            if (value == null) return null;
            return value.Length <= length ? value : value.Substring(0, length);
        }
    }
}
