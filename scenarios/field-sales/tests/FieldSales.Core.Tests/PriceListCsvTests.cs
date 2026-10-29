using System.Globalization;
using System.IO;
using Xunit;

namespace Wingtip.FieldSales.Core.Tests
{
    public class PriceListCsvTests
    {
        private static CultureInfo Finnish()
        {
            var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            culture.NumberFormat.NumberDecimalSeparator = ",";
            culture.NumberFormat.NumberGroupSeparator = " ";
            culture.TextInfo.ListSeparator = ";";
            return culture;
        }

        [Fact]
        public void Reads_regional_csv()
        {
            var csv = "﻿ItemNo;Name;Category;Unit;CaseSize;ListPrice;VatRate;MinOrderQty;Active\n" +
                      "10021;Coffee 500 g;dry;cs;12;48,90;14;1;1\n" +
                      "\n" +
                      "20410;\"Milk 1 l\";DAIRY;CS;10;9,80;14;2;K\n";

            var result = new PriceListCsv(Finnish()).Read(new StringReader(csv));

            Assert.Empty(result.Errors);
            Assert.Equal(2, result.Products.Count);
            Assert.Equal(48.90m, result.Products[0].ListPrice);
            Assert.Equal("DRY", result.Products[0].Category);
            Assert.Equal("Milk 1 l", result.Products[1].Name);
            Assert.True(result.Products[1].Active);
        }

        [Fact]
        public void Reports_bad_lines_with_line_numbers()
        {
            var csv = "ItemNo;Name;Category;Unit;CaseSize;ListPrice;VatRate;MinOrderQty;Active\n" +
                      "10021;Coffee;DRY;BOX;12;48,90;14;1;1\n" +
                      "10022;Tea;DRY;CS;x;5,00;14;1;1\n" +
                      "10023;Cocoa;DRY;CS;12;6,10;14;1;1\n" +
                      "10023;Cocoa;DRY;CS;12;6,10;14;1;1\n";

            var result = new PriceListCsv(Finnish()).Read(new StringReader(csv));

            Assert.Single(result.Products);
            Assert.Equal(3, result.Errors.Count);
            Assert.StartsWith("Line 2:", result.Errors[0]);
            Assert.StartsWith("Line 3:", result.Errors[1]);
            Assert.Contains("repeated", result.Errors[2]);
        }

        [Fact]
        public void Wrong_separator_is_explained()
        {
            var csv = "ItemNo,Name,Category,Unit,CaseSize,ListPrice,VatRate,MinOrderQty,Active\n";
            var result = new PriceListCsv(Finnish()).Read(new StringReader(csv));
            Assert.Contains("list separator", Assert.Single(result.Errors));
        }
    }
}
