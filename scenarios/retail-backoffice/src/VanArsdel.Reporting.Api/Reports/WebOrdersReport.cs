using System.Data.Common;
using Dapper;

namespace VanArsdel.Reporting.Api.Reports;

/// <summary>Web shop order intake per market and delivery method for the last 14 days.</summary>
public class WebOrdersReport : IReport
{
    private const string Sql =
        "SELECT Market, DeliveryMethod, Status, SubmittedUtc, TotalEur, Lines FROM FactWebOrder " +
        "WHERE Channel = 'Web' AND SubmittedUtc >= @from AND SubmittedUtc < @to";

    public string Name => "web-orders";
    public string Title => "Web orders (14 days)";

    public async Task<List<ReportTable>> BuildAsync(DbConnection connection, DateTime businessDate, CancellationToken cancellationToken)
    {
        var to = businessDate.Date.AddDays(1);
        var orders = (await connection.QueryAsync<OrderRow>(new CommandDefinition(Sql, new { from = to.AddDays(-14), to }, cancellationToken: cancellationToken))).ToList();

        var daily = new ReportTable
        {
            Sheet = "Daily",
            Columns =
            {
                new ReportColumn("Date", Formats.Date, 11), new ReportColumn("Market", width: 8), new ReportColumn("Orders", Formats.Integer, 9),
                new ReportColumn("Value EUR", Formats.Money), new ReportColumn("AOV EUR", Formats.Money, 10), new ReportColumn("Units/order", "0.00", 11),
                new ReportColumn("Cancelled", Formats.Percent, 10), new ReportColumn("Click & collect", Formats.Percent, 13),
            },
        };
        foreach (var g in orders.GroupBy(o => (o.SubmittedUtc.Date, o.Market)).OrderBy(g => g.Key.Date).ThenBy(g => g.Key.Market))
        {
            var count = g.Count();
            var value = g.Sum(o => o.TotalEur);
            daily.Rows.Add(new object[]
            {
                g.Key.Date, g.Key.Market, count, value, Math.Round(value / count, 2), Math.Round((decimal)g.Sum(o => o.Lines) / count, 2),
                Formats.Ratio(g.Count(o => o.Status == "Cancelled"), count), Formats.Ratio(g.Count(o => o.DeliveryMethod == "ClickAndCollect"), count),
            });
        }

        var delivery = new ReportTable
        {
            Sheet = "Delivery methods",
            Columns = { new ReportColumn("Method", width: 16), new ReportColumn("Orders", Formats.Integer, 9), new ReportColumn("Share", Formats.Percent, 9), new ReportColumn("Completed", Formats.Percent, 10) },
        };
        foreach (var g in orders.GroupBy(o => o.DeliveryMethod ?? "?").OrderByDescending(g => g.Count()))
        {
            delivery.Rows.Add(new object[] { g.Key, g.Count(), Formats.Ratio(g.Count(), orders.Count), Formats.Ratio(g.Count(o => o.Status == "Completed"), g.Count()) });
        }
        return new List<ReportTable> { daily, delivery };
    }

    private class OrderRow
    {
        public string Market { get; set; }
        public string DeliveryMethod { get; set; }
        public string Status { get; set; }
        public DateTime SubmittedUtc { get; set; }
        public decimal TotalEur { get; set; }
        public int Lines { get; set; }
    }
}
