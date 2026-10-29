using System.Data.Common;
using Dapper;

namespace VanArsdel.Reporting.Api.Reports;

/// <summary>Morning flash for the area managers: yesterday per store and web market, against the same weekday last week.</summary>
public class DailySalesFlashReport : IReport
{
    private const string Sql =
        "SELECT f.LocationCode, COALESCE(s.Name, f.LocationCode) AS Name, f.Market, f.Channel, " +
        "SUM(CASE WHEN f.BusinessDate = @day THEN f.NetAmountEur ELSE 0 END) AS NetEur, " +
        "SUM(CASE WHEN f.BusinessDate = @lastWeek THEN f.NetAmountEur ELSE 0 END) AS NetEurLastWeek, " +
        "SUM(CASE WHEN f.BusinessDate = @day THEN f.Quantity ELSE 0 END) AS Units, " +
        "COUNT(DISTINCT CASE WHEN f.BusinessDate = @day AND f.Quantity > 0 THEN f.SourceRef END) AS Transactions, " +
        "SUM(CASE WHEN f.BusinessDate = @day AND f.Loyalty = 1 THEN f.NetAmountEur ELSE 0 END) AS LoyaltyEur " +
        "FROM FactSales f LEFT JOIN DimStore s ON s.StoreCode = f.LocationCode " +
        "WHERE f.BusinessDate IN (@day, @lastWeek) " +
        "GROUP BY f.LocationCode, s.Name, f.Market, f.Channel " +
        "ORDER BY f.Channel DESC, f.Market, f.LocationCode";

    public string Name => "daily-sales-flash";
    public string Title => "Daily sales flash";

    public async Task<List<ReportTable>> BuildAsync(DbConnection connection, DateTime businessDate, CancellationToken cancellationToken)
    {
        var day = businessDate.Date;
        var rows = (await connection.QueryAsync<FlashRow>(new CommandDefinition(Sql, new { day, lastWeek = day.AddDays(-7) }, cancellationToken: cancellationToken))).ToList();

        var table = new ReportTable
        {
            Sheet = "Flash " + day.ToString("yyyy-MM-dd"),
            Columns =
            {
                new ReportColumn("Location", width: 10), new ReportColumn("Name", width: 28), new ReportColumn("Market", width: 8), new ReportColumn("Channel", width: 9),
                new ReportColumn("Net sales EUR", Formats.Money), new ReportColumn("Same day LW", Formats.Money), new ReportColumn("vs LW", Formats.Percent, 9),
                new ReportColumn("Units", Formats.Integer, 9), new ReportColumn("Transactions", Formats.Integer, 12), new ReportColumn("ATV EUR", Formats.Money, 10),
                new ReportColumn("Loyalty share", Formats.Percent, 12),
            },
        };
        foreach (var r in rows)
        {
            table.Rows.Add(new object[]
            {
                r.LocationCode, r.Name, r.Market, r.Channel, r.NetEur, r.NetEurLastWeek,
                r.NetEurLastWeek == 0 ? 0 : Formats.Ratio(r.NetEur - r.NetEurLastWeek, r.NetEurLastWeek),
                r.Units, r.Transactions, r.Transactions == 0 ? 0 : Math.Round(r.NetEur / r.Transactions, 2), Formats.Ratio(r.LoyaltyEur, r.NetEur),
            });
        }
        var net = rows.Sum(r => r.NetEur);
        var lw = rows.Sum(r => r.NetEurLastWeek);
        var tx = rows.Sum(r => r.Transactions);
        table.Totals = new object[]
        {
            "Total", null, null, null, net, lw, lw == 0 ? 0 : Formats.Ratio(net - lw, lw), rows.Sum(r => r.Units), tx,
            tx == 0 ? 0 : Math.Round(net / tx, 2), Formats.Ratio(rows.Sum(r => r.LoyaltyEur), net),
        };

        var byMarket = new ReportTable
        {
            Sheet = "By market",
            Columns = { new ReportColumn("Market", width: 8), new ReportColumn("Stores EUR", Formats.Money), new ReportColumn("Web EUR", Formats.Money), new ReportColumn("Web share", Formats.Percent, 10) },
        };
        foreach (var g in rows.GroupBy(r => r.Market).OrderBy(g => g.Key))
        {
            var stores = g.Where(r => r.Channel == "Store").Sum(r => r.NetEur);
            var web = g.Where(r => r.Channel == "Web").Sum(r => r.NetEur);
            byMarket.Rows.Add(new object[] { g.Key, stores, web, Formats.Ratio(web, stores + web) });
        }
        return new List<ReportTable> { table, byMarket };
    }

    private class FlashRow
    {
        public string LocationCode { get; set; }
        public string Name { get; set; }
        public string Market { get; set; }
        public string Channel { get; set; }
        public decimal NetEur { get; set; }
        public decimal NetEurLastWeek { get; set; }
        public int Units { get; set; }
        public int Transactions { get; set; }
        public decimal LoyaltyEur { get; set; }
    }
}
