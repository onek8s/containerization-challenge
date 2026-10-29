using System.Globalization;
using FieldService.Data.Entities;

namespace FieldService.Data.Services;

/// <summary>
/// Builds the semicolon separated invoice line file picked up by the finance system.
/// One line per labor entry and one per part used.
/// </summary>
public static class InvoiceExport
{
    public const string Header = "Batch;Account;WorkOrder;CustomerRef;CompletedDate;LineType;Item;Quantity;UnitPrice;Amount";

    public static decimal OrderTotal(WorkOrder order) =>
        Math.Round(order.LaborHours * (order.Customer?.HourlyRate ?? 0m), 2)
        + order.Parts.Sum(p => Math.Round(p.Quantity * p.UnitPrice, 2));

    public static decimal Write(TextWriter writer, string batchNumber, IEnumerable<WorkOrder> orders)
    {
        var inv = CultureInfo.InvariantCulture;
        decimal total = 0;
        writer.WriteLine(Header);
        foreach (var order in orders)
        {
            var account = order.Customer?.AccountCode ?? "";
            var completed = (order.CompletedUtc ?? order.CreatedUtc).ToString("yyyy-MM-dd", inv);
            var rate = order.Customer?.HourlyRate ?? 0m;
            var labor = Math.Round(order.LaborHours * rate, 2);
            writer.WriteLine(string.Join(';', batchNumber, account, order.Number, order.CustomerReference ?? "", completed,
                "LABOR", "LABOR", order.LaborHours.ToString("0.00", inv), rate.ToString("0.00", inv), labor.ToString("0.00", inv)));
            total += labor;

            foreach (var part in order.Parts)
            {
                var amount = Math.Round(part.Quantity * part.UnitPrice, 2);
                writer.WriteLine(string.Join(';', batchNumber, account, order.Number, order.CustomerReference ?? "", completed,
                    "PART", part.PartNumber, part.Quantity.ToString(inv), part.UnitPrice.ToString("0.00", inv), amount.ToString("0.00", inv)));
                total += amount;
            }
        }

        return total;
    }
}
