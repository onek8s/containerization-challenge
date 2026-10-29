using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using VanArsdel.Contracts.Pos;

namespace VanArsdel.PosFeed.Parsing
{
    /// <summary>
    /// Store till export ("TLOG export v3"), one file per store per upload, ISO-8859-1, semicolon separated,
    /// decimal comma (the tills run with the Finnish regional settings in every country).
    ///
    /// H;store;yyyyMMdd;sequence;currency
    /// R;receiptNo;tillNo;yyyy-MM-dd HH:mm:ss;cashierId;loyaltyCard;returnFlag(0/1);total;vatTotal
    /// L;sku;quantity;unitPrice;discount;lineTotal;promotionCode
    /// P;tender;amount
    /// T;receiptCount;grandTotal
    ///
    /// Blank lines and lines starting with # are ignored. File name: POS_{store}_{yyyyMMdd}_{seq}.txt
    /// </summary>
    public static class PosFileParser
    {
        public static readonly Encoding FileEncoding = Encoding.GetEncoding("iso-8859-1");
        private static readonly CultureInfo PosCulture = CultureInfo.GetCultureInfo("fi-FI");

        public static StoreSalesReceived Parse(string fileName, Stream content)
        {
            using (var reader = new StreamReader(content, FileEncoding))
            {
                return Parse(fileName, reader.ReadToEnd());
            }
        }

        public static StoreSalesReceived Parse(string fileName, string text)
        {
            var result = new StoreSalesReceived { FileName = Path.GetFileName(fileName) };
            PosReceipt current = null;
            var headerSeen = false;
            int? trailerCount = null;
            decimal? trailerTotal = null;
            var lineNo = 0;

            foreach (var raw in text.Split('\n'))
            {
                lineNo++;
                var line = raw.TrimEnd('\r');
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                var f = line.Split(';');
                try
                {
                    switch (f[0])
                    {
                        case "H":
                            Require(f, 5, lineNo);
                            result.StoreCode = f[1].Trim().ToUpperInvariant();
                            result.BusinessDate = DateTime.ParseExact(f[2], "yyyyMMdd", CultureInfo.InvariantCulture);
                            result.Sequence = int.Parse(f[3], CultureInfo.InvariantCulture);
                            result.Currency = f[4].Trim().ToUpperInvariant();
                            headerSeen = true;
                            break;
                        case "R":
                            Require(f, 9, lineNo);
                            if (!headerSeen) throw new PosFileException("Receipt before header", lineNo);
                            current = new PosReceipt
                            {
                                ReceiptNo = f[1],
                                TillNo = f[2],
                                Timestamp = DateTime.ParseExact(f[3], "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                                CashierId = f[4],
                                LoyaltyCardNo = string.IsNullOrWhiteSpace(f[5]) ? null : f[5].Trim(),
                                IsReturn = f[6] == "1",
                                Total = Amount(f[7]),
                                VatTotal = Amount(f[8]),
                            };
                            result.Receipts.Add(current);
                            break;
                        case "L":
                            Require(f, 7, lineNo);
                            if (current == null) throw new PosFileException("Line without receipt", lineNo);
                            current.Lines.Add(new PosLine
                            {
                                Sku = f[1].Trim(),
                                Quantity = int.Parse(f[2], CultureInfo.InvariantCulture),
                                UnitPrice = Amount(f[3]),
                                Discount = Amount(f[4]),
                                LineTotal = Amount(f[5]),
                                PromotionCode = string.IsNullOrWhiteSpace(f[6]) ? null : f[6].Trim(),
                            });
                            break;
                        case "P":
                            Require(f, 3, lineNo);
                            if (current == null) throw new PosFileException("Payment without receipt", lineNo);
                            current.Payments.Add(new PosPayment { Tender = f[1].Trim().ToUpperInvariant(), Amount = Amount(f[2]) });
                            break;
                        case "T":
                            Require(f, 3, lineNo);
                            trailerCount = int.Parse(f[1], CultureInfo.InvariantCulture);
                            trailerTotal = Amount(f[2]);
                            break;
                        default:
                            throw new PosFileException("Unknown record type '" + f[0] + "'", lineNo);
                    }
                }
                catch (FormatException ex)
                {
                    throw new PosFileException(ex.Message, lineNo);
                }
            }

            if (!headerSeen) throw new PosFileException("Header record missing", 0);
            if (trailerCount == null) throw new PosFileException("Trailer record missing (file still being written?)", lineNo);
            if (trailerCount != result.Receipts.Count)
            {
                throw new PosFileException($"Trailer says {trailerCount} receipts, file has {result.Receipts.Count}", lineNo);
            }
            var total = result.Receipts.Sum(r => r.IsReturn ? -Math.Abs(r.Total) : r.Total);
            if (Math.Abs(total - trailerTotal.Value) > 0.01m)
            {
                throw new PosFileException($"Trailer total {trailerTotal} does not match receipts {total}", lineNo);
            }
            foreach (var r in result.Receipts)
            {
                var paid = r.Payments.Sum(p => p.Amount);
                if (Math.Abs(paid - r.Total) > 0.01m)
                {
                    throw new PosFileException($"Receipt {r.ReceiptNo} payments {paid} do not match total {r.Total}", lineNo);
                }
            }

            result.BatchId = BatchIdFor(result.FileName, text);
            return result;
        }

        /// <summary>Same file content gives the same batch id, so a re-sent file is ignored downstream.</summary>
        public static Guid BatchIdFor(string fileName, string text)
        {
            using (var md5 = MD5.Create())
            {
                return new Guid(md5.ComputeHash(FileEncoding.GetBytes(fileName.ToUpperInvariant() + "\n" + text)));
            }
        }

        private static decimal Amount(string value) =>
            string.IsNullOrWhiteSpace(value) ? 0m : decimal.Parse(value.Trim(), NumberStyles.Number, PosCulture);

        private static void Require(string[] fields, int count, int lineNo)
        {
            if (fields.Length < count)
            {
                throw new PosFileException($"Record {fields[0]} has {fields.Length} fields, expected {count}", lineNo);
            }
        }
    }

    public class PosFileException : Exception
    {
        public PosFileException(string message, int lineNo) : base(lineNo > 0 ? $"Line {lineNo}: {message}" : message)
        {
            LineNo = lineNo;
        }

        public int LineNo { get; }
    }
}
