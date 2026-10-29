using System;
using System.Collections.Generic;
using System.Linq;
using OfficeOpenXml;
using Tailspin.DealerOrders.Domain;

namespace Tailspin.DealerOrders.Documents.Excel
{
    /// <summary>Order list export for dealers and the sales desk (orders sheet and an order lines sheet).</summary>
    public static class OrderExportWorkbook
    {
        public static byte[] Build(IEnumerable<SalesOrder> orders, Func<int, string> dealerName)
        {
            var list = orders.OrderByDescending(o => o.CreatedUtc).ToList();
            using (var package = new ExcelPackage())
            {
                var sheet = package.Workbook.Worksheets.Add("Orders");
                var headers = new[] { "Order", "Dealer", "Dealer PO", "Type", "Source", "Status", "AX order", "Created", "Requested", "Confirmed", "Currency", "Net value", "Freight" };
                for (var i = 0; i < headers.Length; i++) sheet.Cells[1, i + 1].Value = headers[i];
                sheet.Cells[1, 1, 1, headers.Length].Style.Font.Bold = true;
                var row = 2;
                foreach (var o in list)
                {
                    sheet.Cells[row, 1].Value = o.OrderNumber;
                    sheet.Cells[row, 2].Value = dealerName(o.DealerId);
                    sheet.Cells[row, 3].Value = o.DealerPoNumber;
                    sheet.Cells[row, 4].Value = o.OrderType.ToString();
                    sheet.Cells[row, 5].Value = o.Source.ToString();
                    sheet.Cells[row, 6].Value = o.Status.ToString();
                    sheet.Cells[row, 7].Value = o.AxSalesId;
                    sheet.Cells[row, 8].Value = o.CreatedUtc.ToLocalTime();
                    sheet.Cells[row, 9].Value = o.RequestedDeliveryDate;
                    sheet.Cells[row, 10].Value = o.ConfirmedDeliveryDate;
                    sheet.Cells[row, 11].Value = o.Currency;
                    sheet.Cells[row, 12].Value = o.TotalNet;
                    sheet.Cells[row, 13].Value = o.FreightCharge;
                    row++;
                }
                sheet.Cells[2, 8, Math.Max(2, row), 8].Style.Numberformat.Format = "yyyy-mm-dd hh:mm";
                sheet.Cells[2, 9, Math.Max(2, row), 10].Style.Numberformat.Format = "yyyy-mm-dd";
                sheet.Cells[2, 12, Math.Max(2, row), 13].Style.Numberformat.Format = "#,##0.00";

                var lines = package.Workbook.Worksheets.Add("Lines");
                var lineHeaders = new[] { "Order", "Line", "Item", "Configuration", "Options", "Description", "Quantity", "Net price", "Shipped", "Ship date", "Serial" };
                for (var i = 0; i < lineHeaders.Length; i++) lines.Cells[1, i + 1].Value = lineHeaders[i];
                lines.Cells[1, 1, 1, lineHeaders.Length].Style.Font.Bold = true;
                var r = 2;
                foreach (var o in list)
                {
                    foreach (var l in o.Lines.OrderBy(x => x.LineNumber))
                    {
                        lines.Cells[r, 1].Value = o.OrderNumber;
                        lines.Cells[r, 2].Value = l.LineNumber;
                        lines.Cells[r, 3].Value = l.ItemId;
                        lines.Cells[r, 4].Value = l.ConfigurationId;
                        lines.Cells[r, 5].Value = l.OptionCodes;
                        lines.Cells[r, 6].Value = l.Description;
                        lines.Cells[r, 7].Value = l.Quantity;
                        lines.Cells[r, 8].Value = l.NetPrice;
                        lines.Cells[r, 9].Value = l.ShippedQuantity;
                        lines.Cells[r, 10].Value = l.ConfirmedShipDate;
                        lines.Cells[r, 11].Value = l.SerialNumber;
                        r++;
                    }
                }
                lines.Cells[2, 10, Math.Max(2, r), 10].Style.Numberformat.Format = "yyyy-mm-dd";
                if (sheet.Dimension != null) sheet.Cells[sheet.Dimension.Address].AutoFitColumns();
                if (lines.Dimension != null) lines.Cells[lines.Dimension.Address].AutoFitColumns();
                return package.GetAsByteArray();
            }
        }
    }
}
