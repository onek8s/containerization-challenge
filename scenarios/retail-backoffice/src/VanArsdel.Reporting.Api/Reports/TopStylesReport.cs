using System.Data.Common;
using Dapper;

namespace VanArsdel.Reporting.Api.Reports;

/// <summary>Best and worst sellers of the last seven days with return rates, for the Monday trading meeting.</summary>
public class TopStylesReport : IReport
{
    private const string SalesSql =
        "SELECT COALESCE(p.StyleCode, f.Sku) AS StyleCode, MAX(p.Name) AS Name, MAX(p.Brand) AS Brand, MAX(p.CategoryCode) AS Category, " +
        "SUM(CASE WHEN f.Quantity > 0 THEN f.Quantity ELSE 0 END) AS Units, SUM(f.NetAmountEur) AS NetEur, " +
        "SUM(CASE WHEN f.Channel = 'Web' THEN f.Quantity ELSE 0 END) AS WebUnits " +
        "FROM FactSales f LEFT JOIN DimProduct p ON p.Sku = f.Sku " +
        "WHERE f.BusinessDate > @from AND f.BusinessDate <= @to " +
        "GROUP BY COALESCE(p.StyleCode, f.Sku)";

    private const string ReturnsSql =
        "SELECT COALESCE(p.StyleCode, r.Sku) AS StyleCode, SUM(r.Quantity) AS Units " +
        "FROM FactReturn r LEFT JOIN DimProduct p ON p.Sku = r.Sku " +
        "WHERE r.BusinessDate > @from AND r.BusinessDate <= @to GROUP BY COALESCE(p.StyleCode, r.Sku)";

    public string Name => "top-styles";
    public string Title => "Top and bottom styles (7 days)";

    public async Task<List<ReportTable>> BuildAsync(DbConnection connection, DateTime businessDate, CancellationToken cancellationToken)
    {
        var args = new { from = businessDate.Date.AddDays(-7), to = businessDate.Date };
        var sales = (await connection.QueryAsync<StyleRow>(new CommandDefinition(SalesSql, args, cancellationToken: cancellationToken))).ToList();
        var returns = (await connection.QueryAsync<(string StyleCode, int Units)>(new CommandDefinition(ReturnsSql, args, cancellationToken: cancellationToken)))
            .ToDictionary(r => r.StyleCode, r => r.Units);

        ReportTable Table(string sheet, IEnumerable<StyleRow> rows)
        {
            var t = new ReportTable
            {
                Sheet = sheet,
                Columns =
                {
                    new ReportColumn("Rank", width: 6), new ReportColumn("Style", width: 10), new ReportColumn("Name", width: 30), new ReportColumn("Brand", width: 8),
                    new ReportColumn("Category", width: 10), new ReportColumn("Units", Formats.Integer, 9), new ReportColumn("Net EUR", Formats.Money),
                    new ReportColumn("Web share", Formats.Percent, 10), new ReportColumn("Returned", Formats.Integer, 9), new ReportColumn("Return rate", Formats.Percent, 11),
                },
            };
            var rank = 1;
            foreach (var r in rows)
            {
                var returned = returns.TryGetValue(r.StyleCode, out var u) ? u : 0;
                t.Rows.Add(new object[] { rank++, r.StyleCode, r.Name, r.Brand, r.Category, r.Units, r.NetEur, Formats.Ratio(r.WebUnits, r.Units), returned, Formats.Ratio(returned, r.Units) });
            }
            return t;
        }

        return new List<ReportTable>
        {
            Table("Top 50", sales.OrderByDescending(s => s.NetEur).Take(50)),
            Table("Bottom 50", sales.Where(s => s.Units > 0).OrderBy(s => s.NetEur).Take(50)),
        };
    }

    private class StyleRow
    {
        public string StyleCode { get; set; }
        public string Name { get; set; }
        public string Brand { get; set; }
        public string Category { get; set; }
        public int Units { get; set; }
        public decimal NetEur { get; set; }
        public int WebUnits { get; set; }
    }
}
