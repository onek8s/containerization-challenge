using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using Tailspin.DealerOrders.Configurator;
using Tailspin.DealerOrders.Domain;

namespace Tailspin.DealerOrders.Documents.Excel
{
    /// <summary>
    /// Dealer price list workbook: one sheet per product family with the base machines, one sheet with all options per
    /// model, and a cover sheet with the dealer's terms. Prices are the dealer's own net prices on the given date.
    /// </summary>
    public class PriceListWorkbook
    {
        private readonly PriceCalculator _calculator;

        public PriceListWorkbook(PriceCalculator calculator)
        {
            _calculator = calculator;
        }

        public byte[] Build(Dealer dealer, IEnumerable<ProductModel> models, DateTime date)
        {
            var list = models.Where(m => m.IsValidOn(date) && m.IsAvailableIn(dealer.CountryCode)).OrderBy(m => m.Family).ThenBy(m => m.ModelCode).ToList();
            using (var package = new ExcelPackage())
            {
                package.Workbook.Properties.Title = $"Tailspin Machinery price list {date:yyyy-MM-dd}";
                package.Workbook.Properties.Author = "Tailspin Machinery Dealer Orders";
                package.Workbook.Properties.Company = "Tailspin Machinery";

                Cover(package.Workbook.Worksheets.Add("Terms"), dealer, date);

                foreach (var family in list.GroupBy(m => m.Family))
                {
                    var sheet = package.Workbook.Worksheets.Add(family.Key.ToString());
                    Header(sheet, "Model", "Item", "Name", "Model year", "Lead time (weeks)", "List price", "Discount %", "Net price", "Currency");
                    var row = 2;
                    foreach (var model in family)
                    {
                        var config = new MachineConfiguration(model.ModelCode, Enumerable.Empty<string>());
                        var price = _calculator.Calculate(model, config, dealer, 1, 0, dealer.CountryCode, date);
                        var line = price.Lines.First();
                        sheet.Cells[row, 1].Value = model.ModelCode;
                        sheet.Cells[row, 2].Value = model.ItemId;
                        sheet.Cells[row, 3].Value = model.Name;
                        sheet.Cells[row, 4].Value = model.ModelYear;
                        sheet.Cells[row, 5].Value = model.LeadTimeWeeks;
                        sheet.Cells[row, 6].Value = line.ListPrice;
                        sheet.Cells[row, 7].Value = line.DiscountPercent / 100m;
                        sheet.Cells[row, 8].Formula = $"ROUND(F{row}*(1-G{row}),{Currencies.Decimals(dealer.Currency)})";
                        sheet.Cells[row, 9].Value = dealer.Currency;
                        row++;
                    }
                    Format(sheet, row - 1, dealer.Currency, 6, 8);
                }

                var options = package.Workbook.Worksheets.Add("Options");
                Header(options, "Model", "Group", "Option", "Item", "Name", "Extra lead time", "List price", "Discount %", "Net price", "Currency");
                var r = 2;
                foreach (var model in list)
                {
                    foreach (var group in model.OptionGroups.OrderBy(g => g.SortOrder))
                    {
                        foreach (var option in group.Options.Where(o => o.IsAvailableIn(dealer.CountryCode) && !string.IsNullOrEmpty(o.ItemId)))
                        {
                            var config = new MachineConfiguration(model.ModelCode, new[] { option.Code });
                            var price = _calculator.Calculate(model, config, dealer, 1, 0, dealer.CountryCode, date);
                            var line = price.Lines.FirstOrDefault(l => l.Code == option.Code);
                            if (line == null) continue;
                            options.Cells[r, 1].Value = model.ModelCode;
                            options.Cells[r, 2].Value = group.Name;
                            options.Cells[r, 3].Value = option.Code;
                            options.Cells[r, 4].Value = option.ItemId;
                            options.Cells[r, 5].Value = option.Name;
                            options.Cells[r, 6].Value = option.ExtraLeadTimeWeeks == 0 ? null : (object)option.ExtraLeadTimeWeeks;
                            options.Cells[r, 7].Value = line.ListPrice;
                            options.Cells[r, 8].Value = line.DiscountPercent / 100m;
                            options.Cells[r, 9].Formula = $"ROUND(G{r}*(1-H{r}),{Currencies.Decimals(dealer.Currency)})";
                            options.Cells[r, 10].Value = dealer.Currency;
                            r++;
                        }
                    }
                }
                Format(options, r - 1, dealer.Currency, 7, 9);
                return package.GetAsByteArray();
            }
        }

        private static void Cover(ExcelWorksheet sheet, Dealer dealer, DateTime date)
        {
            sheet.Cells["A1"].Value = "Tailspin Machinery dealer price list";
            sheet.Cells["A1"].Style.Font.Size = 16;
            sheet.Cells["A1"].Style.Font.Bold = true;
            var rows = new[]
            {
                new[] { "Dealer", dealer.Name },
                new[] { "Account", dealer.AccountNumber },
                new[] { "Price group", dealer.PriceGroup },
                new[] { "Discount tier", dealer.DiscountTier.ToString() },
                new[] { "Currency", dealer.Currency },
                new[] { "Payment terms", dealer.PaymentTerms },
                new[] { "Delivery terms", dealer.DeliveryTerms.ToString().ToUpperInvariant() },
                new[] { "Prices valid on", date.ToString("yyyy-MM-dd") },
                new[] { "Generated", DateTime.Now.ToString("yyyy-MM-dd HH:mm") }
            };
            for (var i = 0; i < rows.Length; i++)
            {
                sheet.Cells[i + 3, 1].Value = rows[i][0];
                sheet.Cells[i + 3, 1].Style.Font.Bold = true;
                sheet.Cells[i + 3, 2].Value = rows[i][1];
            }
            sheet.Cells[rows.Length + 4, 1].Value = "Prices exclude VAT. Freight is charged separately for CPT and DAP deliveries.";
            sheet.Column(1).Width = 18;
            sheet.Column(2).Width = 40;
        }

        private static void Header(ExcelWorksheet sheet, params string[] titles)
        {
            for (var i = 0; i < titles.Length; i++) sheet.Cells[1, i + 1].Value = titles[i];
            using (var range = sheet.Cells[1, 1, 1, titles.Length])
            {
                range.Style.Font.Bold = true;
                range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                range.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(0x1F, 0x4E, 0x79));
                range.Style.Font.Color.SetColor(Color.White);
            }
            sheet.View.FreezePanes(2, 1);
        }

        private static void Format(ExcelWorksheet sheet, int lastRow, string currency, int listColumn, int netColumn)
        {
            if (lastRow < 2) return;
            var format = Currencies.Decimals(currency) == 0 ? "#,##0" : "#,##0.00";
            sheet.Cells[2, listColumn, lastRow, listColumn].Style.Numberformat.Format = format;
            sheet.Cells[2, listColumn + 1, lastRow, listColumn + 1].Style.Numberformat.Format = "0.0%";
            sheet.Cells[2, netColumn, lastRow, netColumn].Style.Numberformat.Format = format;
            sheet.Cells[1, 1, lastRow, sheet.Dimension.End.Column].AutoFilter = true;
            sheet.Cells[sheet.Dimension.Address].AutoFitColumns(8, 60);
        }
    }
}
