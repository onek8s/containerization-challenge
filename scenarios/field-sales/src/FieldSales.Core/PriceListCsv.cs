using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Wingtip.FieldSales.Core
{
    public class PriceListImport
    {
        public PriceListImport()
        {
            Products = new List<Product>();
            Errors = new List<string>();
        }

        public List<Product> Products { get; private set; }
        public List<string> Errors { get; private set; }
    }

    /// <summary>
    /// Reads the price list exported from the purchasing spreadsheet ("Save as CSV" in Excel), which uses the
    /// regional list separator and decimal separator of the purchasing office.
    /// Columns: ItemNo;Name;Category;Unit;CaseSize;ListPrice;VatRate;MinOrderQty;Active
    /// </summary>
    public class PriceListCsv
    {
        private static readonly string[] Columns =
            { "ItemNo", "Name", "Category", "Unit", "CaseSize", "ListPrice", "VatRate", "MinOrderQty", "Active" };

        private readonly CultureInfo _culture;
        private readonly char _separator;

        public PriceListCsv(CultureInfo culture)
        {
            _culture = culture ?? CultureInfo.CurrentCulture;
            var listSeparator = _culture.TextInfo.ListSeparator;
            _separator = string.IsNullOrEmpty(listSeparator) ? ';' : listSeparator[0];
        }

        public PriceListImport Read(TextReader reader)
        {
            var result = new PriceListImport();
            var header = reader.ReadLine();
            if (header == null)
            {
                result.Errors.Add("The file is empty.");
                return result;
            }

            var names = header.Trim('﻿').Split(_separator).Select(h => h.Trim()).ToArray();
            var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < names.Length; i++) index[names[i]] = i;

            var missing = Columns.Where(c => !index.ContainsKey(c)).ToList();
            if (missing.Count > 0)
            {
                result.Errors.Add("Missing columns: " + string.Join(", ", missing) + ". Check the list separator (" + _separator + ").");
                return result;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string line;
            var lineNo = 1;
            while ((line = reader.ReadLine()) != null)
            {
                lineNo++;
                if (line.Trim().Length == 0) continue;

                var f = line.Split(_separator);
                Func<string, string> get = c => index[c] < f.Length ? f[index[c]].Trim().Trim('"') : "";

                try
                {
                    var product = new Product
                    {
                        ItemNo = get("ItemNo").ToUpperInvariant(),
                        Name = get("Name"),
                        Category = get("Category").ToUpperInvariant(),
                        Unit = get("Unit").ToUpperInvariant(),
                        CaseSize = int.Parse(get("CaseSize"), NumberStyles.Integer, _culture),
                        ListPrice = decimal.Parse(get("ListPrice"), NumberStyles.Number, _culture),
                        VatRate = decimal.Parse(get("VatRate"), NumberStyles.Number, _culture),
                        MinOrderQty = int.Parse(get("MinOrderQty"), NumberStyles.Integer, _culture),
                        Active = ParseFlag(get("Active"))
                    };

                    if (product.ItemNo.Length == 0) throw new FormatException("ItemNo is empty");
                    if (product.Unit != "EA" && product.Unit != "CS") throw new FormatException("Unit must be EA or CS");
                    if (product.ListPrice < 0m) throw new FormatException("ListPrice is negative");
                    if (!seen.Add(product.ItemNo)) throw new FormatException("ItemNo " + product.ItemNo + " is repeated");

                    result.Products.Add(product);
                }
                catch (FormatException ex)
                {
                    result.Errors.Add("Line " + lineNo + ": " + ex.Message);
                }
                catch (OverflowException ex)
                {
                    result.Errors.Add("Line " + lineNo + ": " + ex.Message);
                }
            }

            return result;
        }

        private static bool ParseFlag(string value)
        {
            switch ((value ?? "").ToUpperInvariant())
            {
                case "1": case "Y": case "YES": case "TRUE": case "K": case "KYLLÄ": return true;
                case "0": case "N": case "NO": case "FALSE": case "E": case "EI": case "": return false;
                default: throw new FormatException("Active must be 1 or 0");
            }
        }
    }
}
