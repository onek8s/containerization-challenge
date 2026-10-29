using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace ProductReturns.Core
{
    /// <summary>
    /// Refund batch for the finance team. Finance imports it into the payment run, so the
    /// layout is fixed: semicolon separated, header row, decimal point, CRLF, ISO-8859-1.
    /// </summary>
    public static class RefundBatchFile
    {
        public const string Header = "BATCH;RMA;ORDER;CUSTOMER;EMAIL;CURRENCY;LINES;RESTOCKING;LABEL;REFUND;INSPECTED;APPROVED_BY";

        public static string FileName(string batchId) => "REFUND_" + batchId + ".csv";

        public static string BatchId(DateTime localNow) => localNow.ToString("yyyyMMdd_HHmm", CultureInfo.InvariantCulture);

        public static Encoding FileEncoding => Encoding.GetEncoding(28591);

        public static void Write(TextWriter writer, string batchId, IEnumerable<ReturnRequest> requests)
        {
            writer.NewLine = "\r\n";
            writer.WriteLine(Header);
            foreach (var r in requests)
            {
                var lines = r.Lines.Where(l => l.ReceivedQuantity > 0)
                    .Sum(l => Math.Round(Math.Min(l.ReceivedQuantity, l.Quantity) * l.UnitPrice, 2, MidpointRounding.AwayFromZero));
                writer.WriteLine(string.Join(";", new[]
                {
                    batchId,
                    r.RmaNumber,
                    r.OrderNumber,
                    Clean(r.CustomerName),
                    Clean(r.CustomerEmail),
                    r.Currency,
                    Amount(lines),
                    Amount(r.RestockingFee ?? 0m),
                    Amount(r.LabelFee ?? 0m),
                    Amount(r.RefundAmount ?? 0m),
                    r.InspectedUtc.HasValue ? r.InspectedUtc.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : string.Empty,
                    Clean(r.DecidedBy)
                }));
            }
        }

        private static string Amount(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

        private static string Clean(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Replace(';', ',').Replace('\r', ' ').Replace('\n', ' ').Trim();
        }
    }
}
